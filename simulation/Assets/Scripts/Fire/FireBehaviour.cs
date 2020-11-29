﻿using Mapbox.Utils;
using Mapbox.Unity.Map;
using System;
using System.Net.Http;
using System.Globalization;
using System.Threading.Tasks;
using UnityEngine;

public class FireBehaviour : MonoBehaviour
{
    public AbstractMap Map;
    public Vector3 IgnitionPoint;
    Vector3 North = new Vector3(0, 0, 1);
    static readonly HttpClient client = new HttpClient();
    private const string ModelNumberUrl =
        "http://127.0.0.1:5000/model-number?lat={0}&lon={1}";
    private const string ModelParametersUrl =
        "http://127.0.0.1:5000/model-parameters?number={0}";
    private const string MoistureUrl =
        "http://127.0.0.1:5500/live-fuel-moisture-content?lat={0}&lon={1}";
    private const string WindSpeedUrl =
        "http://127.0.0.1:6000/weather-data?lat={0}&lon={1}";


    private void Awake()
    {
        Map = UnityEngine.Object.FindObjectOfType<AbstractMap>();
    }

    // Use this for initialization
    void Start()
    {
        PrintStatus();
    }

    async void PrintStatus()
    {
        var rateOfSpreadNoWindSlope = await RateOfSpreadNoWindSlope(IgnitionPoint);
        var rateOfSpreadSameWindSlope = await RateOfSpreaUpslopeWind(IgnitionPoint);
        var rateOfMaximumSpread = await RateOfMaximumSpread(IgnitionPoint);
        WeatherModel weather = await MidflameWindSpeed(IgnitionPoint);
        var windFactor = await WindFactor(IgnitionPoint, weather.current);
        var slopeFactor = await SlopeFactor(IgnitionPoint);
        var slopeInDegrees = GetSlopeInDegrees(GetHitInfo(IgnitionPoint));

        print($"Rate of spread no wind or slope: {rateOfSpreadNoWindSlope}");
        print($"Rate of spread upslope wind: {rateOfSpreadSameWindSlope.spreadRate}");
        print($"Bearing of upslope wind: {rateOfSpreadSameWindSlope.spreadBearing}");
        print($"Rate of maximum spread: {rateOfMaximumSpread.spreadRate}");
        print($"Bearing of maximum spread: {rateOfMaximumSpread.spreadBearing}");
        print($"Wind bearing: {weather.current.wind_deg}");
        print($"Wind speed: {weather.current.wind_speed}");
        print($"Wind factor: {windFactor}");
        print($"Slope in degrees: {slopeInDegrees}");
        print($"Slope bearing: {GetSlopeBearingInDegrees(GetHitInfo(IgnitionPoint))}");
        print($"Slope factor: {slopeFactor}");
    }

    // Update is called once per frame
    void Update()
    {
    }

    /// <summary>
    /// Returns the rate of spread of fire in ft/min given no wind or slope.
    /// </summary>
    /// <param name="point">the unity point in game space</param>
    /// <returns></returns>
    private async Task<float> RateOfSpreadNoWindSlope(Vector3 point)
    {
        FuelModel model = await FuelModelParameters(point);
        float fuelMoisture = await FuelMoistureContent(point);

        // Fuel Particle
        int heatContent = FuelModel.heat_content;
        float totalMineralContent = FuelModel.total_mineral_content;
        float effectiveMineralContent = FuelModel.effective_mineral_content;
        float particleDensity = FuelModel.particle_density;

        // Fuel Array
        float surfaceAreaToVolumeRatio = model.characteristic_sav;
        float ovenDryFuelLoad = model.oven_dry_fuel_load;
        float fuelBedDepth = model.fuel_bed_depth;
        float deadFuelMoistureOfExtinction = model.dead_fuel_moisture_of_extinction;

        float propFluxNoWindSlope = await PropagatingFluxNoWindSlope(point);

        float heatSink = HeatSink(fuelMoisture, model);

        return propFluxNoWindSlope / heatSink;
    }

    /// <summary>
    /// Returns the rate of spread of fire in ft/min given upslope wind.
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    private async Task<SpreadModel> RateOfSpreaUpslopeWind(Vector3 point)
    {
        FuelModel model = await FuelModelParameters(point);
        WeatherModel weatherModel = await MidflameWindSpeed(point);

        float r0 = await RateOfSpreadNoWindSlope(point); // zero-wind, zero-slope rate of spread
        Wind currentWind = weatherModel.current;
        float windFactor = await WindFactor(point, currentWind); // use current wind speed
        float slopeFactor = await SlopeFactor(point);

        SpreadModel spread =
                    new SpreadModel(
                        r0 * (1f + windFactor + slopeFactor),
                        GetSlopeBearingInDegrees(GetHitInfo(point))
                    );

        return spread;
    }

    private async Task<SpreadModel> RateOfMaximumSpread(Vector3 point)
    {
        WeatherModel weatherModel = await MidflameWindSpeed(point);
        Wind currentWind = weatherModel.current;

        float r0 = await RateOfSpreadNoWindSlope(point);
        float slopeBearing = GetSlopeBearingInDegrees(GetHitInfo(point));
        float windBearing = currentWind.wind_deg;

        /* for elapsed time t, the slope vector has magnitude Ds and direction 0.
         * The wind vector has magnitude Dw in direction w from the upslope.
         * the slope vector is (Ds, 0) and the wind vector is (Dwcosw, Dwsinw). 
         * The resultant vector is then (Ds + Dwcosw, Dwsinw). 
         * The magnitude of the head fire vector is Dh in direction a.
         */
        float Ds = r0 * await SlopeFactor(point);
        float Dw = r0 * await WindFactor(point, currentWind);
        float w = Mathf.Abs(slopeBearing - windBearing);

        float X = Ds + (Dw * Mathf.Cos(DegreesToRadians(w)));
        float Y = Dw * Mathf.Sin(DegreesToRadians(w));
        float Dh = (float) Math.Pow(Math.Pow(X, 2f) + Math.Pow(Y, 2f), 0.5f);
        
        float a = Mathf.Asin(DegreesToRadians(Mathf.Abs(Y) / Dh));
        // calculate a relative to North bearing
        if (slopeBearing >= windBearing)
        {
            a = slopeBearing - a;
        } else
        {
            a += slopeBearing;
        }
        float Rh = r0 + (Dh / 1f); // t = 1

        return new SpreadModel(Rh, a);
    }

    #region Heat Sink
    /// <summary>
    /// </summary>
    /// <param name="Mf">moisture content</param>
    /// <param name="model">fuel model</param>
    /// <returns></returns>
    private float HeatSink(float Mf, FuelModel model)
    {
        return FuelModel.particle_density *
                EffectiveHeatingNumber(model.characteristic_sav) *
                HeatOfPreignition(Mf);
    }

    /// <summary>
    /// </summary>
    /// <param name="sigma">surface-area-to-volume-ratio</param>
    /// <returns></returns>
    private float EffectiveHeatingNumber(float sigma)
    {
        return Mathf.Exp(- 138 / sigma);
    }

    /// <summary>
    /// </summary>
    /// <param name="Mf">moisture content</param>
    /// <returns></returns>
    private float HeatOfPreignition(float Mf)
    {
        return 250 + (1116 * Mf);
    }
    #endregion

    #region Heat Source
    /// <summary>
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    private async Task<float> HeatSource(Vector3 point)
    {
        float Mf = await FuelMoistureContent(point);
        FuelModel model = await FuelModelParameters(point);
        float propFlux = await PropagatingFluxNoWindSlope(point);
        Wind wind = (await MidflameWindSpeed(point)).current;

        return propFlux * (1f + await SlopeFactor(point) + await WindFactor(point, wind));
    }

    /// <summary>
    /// </summary>
    /// <param name="point"></param>
    /// <returns>no-wind, no-slope propagating flux</returns>
    private async Task<float> PropagatingFluxNoWindSlope(Vector3 point)
    {
        FuelModel model = await FuelModelParameters(point);
        return await ReactionIntensity(point) * PropagatingFluxRatio(model);
    }

    /// <summary>
    /// </summary>
    /// <param name="point"></param>
    /// <returns></returns>
    private async Task<float> ReactionIntensity(Vector3 point)
    {
        float Mf = await FuelMoistureContent(point);
        FuelModel model = await FuelModelParameters(point);
        float wn = NetFuelLoad(model.oven_dry_fuel_load);
        float nM = MoistureDampingCoefficient(Mf, model.dead_fuel_moisture_of_extinction);

        return OptimumReactionVelocity(model) *
                wn *
                FuelModel.heat_content *
                nM *
                MineralDampingCoefficient();
    }

    /// <summary>
    /// </summary>
    /// <param name="model">fuel model</param>
    /// <returns></returns>
    private float OptimumReactionVelocity(FuelModel model)
    {
        float A = 133f * Mathf.Pow(model.characteristic_sav, -0.7913f);

        return MaximumReactionVelocity(model.characteristic_sav) *
                Mathf.Pow(model.relative_packing_ratio, A) *
                Mathf.Exp(A * (1f - model.relative_packing_ratio));
    }

    /// <summary>
    /// </summary>
    /// <param name="sigma">surface-area-to-volume-ratio</param>
    /// <returns></returns>
    private float MaximumReactionVelocity(float sigma)
    {
        return Mathf.Pow(sigma, 1.5f) *
                Mathf.Pow(495f + (0.0594f * Mathf.Pow(sigma, 1.5f)), -1f);
    }

    /// <summary>
    /// </summary>
    /// <param name="model">fuel model</param>
    /// <returns></returns>
    private float PropagatingFluxRatio(FuelModel model)
    {
        float beta = MeanPackingRatio(model);

        return Mathf.Pow(192f + (0.2595f * model.characteristic_sav), -1f) *
            Mathf.Exp((0.792f + (0.681f * Mathf.Pow(model.characteristic_sav, 0.5f))) * (beta + 0.1f));
    }

    /// <summary>
    /// </summary>
    /// <param name="w0">oven dry fuel load</param>
    /// <param name="sT">total mineral content</param>
    /// <returns></returns>
    private float NetFuelLoad(float w0)
    {
        return w0 * (1 - FuelModel.total_mineral_content);
    }

    /// <summary>
    /// </summary>
    /// <param name="Se">effective mineral content</param>
    /// <returns></returns>
    private float MineralDampingCoefficient()
    {
        float coefficient = 0.174f * Mathf.Pow(FuelModel.effective_mineral_content, -0.19f);
        return Mathf.Min(coefficient, 1f); // (max = 1)
    }

    /// <summary>
    /// </summary>
    /// <param name="Mf">moisture content</param>
    /// <param name="Mx">dead fuel moisture of extinction</param>
    /// <returns></returns>
    private float MoistureDampingCoefficient(float Mf, float Mx)
    {
        float rM = Mathf.Min((Mf / Mx), 1); // (max = 1)

        return (1f - (2.59f * rM)) +
                (5.11f * Mathf.Pow(rM, 2f)) -
                (3.52f * Mathf.Pow(rM, 3f));
    }

    /// <summary>
    /// </summary>
    /// <param name="model">fuel model</param>
    /// <returns></returns>
    private float MeanPackingRatio(FuelModel model)
    {
        return (1 / model.fuel_bed_depth) * (model.oven_dry_fuel_load / FuelModel.particle_density);
    }

    /// <summary>
    /// </summary>
    /// <param name="sigma">surface-area-to-volume-ratio</param>
    /// <returns></returns>
    private float OptimumPackingRatio(float sigma)
    {
        return 3.348f * Mathf.Pow(sigma, -0.8189f);
    }

    /// <summary>
    /// </summary>
    /// <param name="delta">fuel bed depth</param>
    /// <param name="w0"><oven-dry fuel load/param>
    /// <param name="pP">particle density</param>
    /// <param name="sigma">surface-area-to-volume-ratio</param>
    /// <returns></returns>
    private float RelativePackingRatio(FuelModel model)
    {
        return MeanPackingRatio(model) / OptimumPackingRatio(model.characteristic_sav);
    }
    #endregion

    #region Environmental

    /// <summary>
    /// Rate of spread is modelled as constant for wind speeds
    /// greater than the maximum reliable wind speed.
    /// </summary>
    /// <param name="iR">reaction intensity</param>
    /// <returns></returns>
    private float MaximumReliableWindSpeed(float iR)
    {
        return 0.9f * iR;
    }

    /// <summary>
    /// </summary>
    /// <param name="theta">slope angle</param>
    /// <param name="beta">packing ratio</param>
    /// <returns></returns>
    private async Task<float> SlopeFactor(Vector3 point)
    {
        FuelModel model = await FuelModelParameters(point);
        RaycastHit hitInfo = GetHitInfo(point);
        float theta = GetSlopeInDegrees(hitInfo);

        return 5.27f * Mathf.Pow(model.packing_ratio, -0.3f) * Mathf.Pow(Mathf.Tan(DegreesToRadians(theta)), 2f);
    }

    /// <summary>
    /// </summary>
    /// <param name="wind">midflame wind speed</param>
    /// <param name="model"></param>
    /// <returns></returns>
    private async Task<float> WindFactor(Vector3 point, Wind wind)
    {
        FuelModel model = await FuelModelParameters(point);
        float fuelMoisture = await FuelMoistureContent(point);

        float C = 7.47f * Mathf.Exp(-0.133f * Mathf.Pow(model.characteristic_sav, 0.55f));
        float B = 0.025256f * Mathf.Pow(model.characteristic_sav, 0.54f);
        float E = 0.715f * Mathf.Exp(-3.59f * model.characteristic_sav * Mathf.Pow(10f, -4f));
        float U =
                Mathf.Min(
                    MaximumReliableWindSpeed(await ReactionIntensity(point)),
                    wind.wind_speed
                );

        return (float)(C * Mathf.Pow(wind.wind_speed, B) * Math.Pow(model.relative_packing_ratio, -E));
    }

    float GetSlopeInDegrees(RaycastHit hitInfo)
    {
        Vector3 normal = hitInfo.normal;
        return Vector3.Angle(normal, Vector3.up);
    }

    float GetSlopeBearingInDegrees(RaycastHit hitInfo)
    {
        Vector3 normal = hitInfo.normal;

        Vector3 left = Vector3.Cross(normal, Vector3.down);
        Vector3 upslope = Vector3.Cross(normal, left);
        Vector3 upslopeFlat = new Vector3(upslope.x, 0, upslope.z).normalized;

        return BearingBetweenInDegrees(North, upslopeFlat);
    }
    #endregion

    #region utils

    float BearingBetweenInDegrees(Vector3 a, Vector3 b)
    {
        Vector3 normal = Vector3.up;
        // angle in [0, 180]
        float angle = Vector3.Angle(a, b);
        float sign = Mathf.Sign(Vector3.Dot(normal, Vector3.Cross(a, b)));

        // angle in [-179, 180]
        float signedAngle = angle * sign;

        // angle in [0, 360]
        float bearing = (signedAngle + 360) % 360;
        return bearing;
    }

    RaycastHit GetHitInfo(Vector3 point)
    {
        Vector3 origin = new Vector3(point.x, point.y + 100, point.z);

        RaycastHit hitInfo;
        if (Physics.Raycast(origin, Vector3.down, out hitInfo, Mathf.Infinity))
        {
            return hitInfo;
        }
        else throw new Exception("No Hit in Raycast.");
    }

    Vector3 GetVector3FromVector2(Vector3 point)
    {
        Vector2d latlon = Map.WorldToGeoPosition(new Vector3(point.x, 0, point.z));
        Vector3 newPoint = new Vector3(point.x, Map.QueryElevationInUnityUnitsAt(latlon), point.y);
        return newPoint;
    }

    private float DegreesToRadians(float deg)
    {
        return (Mathf.PI / 180f) * deg;
    }

    private float RadiansToDegrees(float rad)
    {
        return (180f / Mathf.PI) * rad;
    }

    #endregion

    #region api calls
    async Task<int> FuelModelNumber(Vector3 point)
    {
        HttpResponseMessage response;

        Vector2d latlon = Map.WorldToGeoPosition(point);

        response = await client.GetAsync(string.Format(ModelNumberUrl, latlon.x, latlon.y));
        response.EnsureSuccessStatusCode();
        string modelNumber = await response.Content.ReadAsStringAsync();
        return Int32.Parse(modelNumber);
    }

    async Task<FuelModel> FuelModelParameters(Vector3 point)
    {
        HttpResponseMessage response;
        Vector2d latlon = Map.WorldToGeoPosition(point);

        int modelNumber = await FuelModelNumber(point);
        response = await client.GetAsync(string.Format(ModelParametersUrl, modelNumber));
        response.EnsureSuccessStatusCode();

        return
            JsonUtility.FromJson<FuelModel>(
                        await response.Content.ReadAsStringAsync()
            );
    }

    async Task<float> FuelMoistureContent(Vector3 point)
    {
        HttpResponseMessage response;
        Vector2d latlon = Map.WorldToGeoPosition(point);

        response = await client.GetAsync(string.Format(MoistureUrl, latlon.x, latlon.y));
        response.EnsureSuccessStatusCode();
        return
            float.Parse(
                await response.Content.ReadAsStringAsync(),
                CultureInfo.InvariantCulture.NumberFormat
            );
    }

    async Task<WeatherModel> MidflameWindSpeed(Vector3 point)
    {
        HttpResponseMessage response;
        Vector2d latlon = Map.WorldToGeoPosition(point);

        response = await client.GetAsync(string.Format(WindSpeedUrl, latlon.x, latlon.y));
        response.EnsureSuccessStatusCode();
        string jsonString = await response.Content.ReadAsStringAsync();

        var jObject = JsonUtility.FromJson<WeatherModel>(jsonString);
        return jObject;
    }
    #endregion
}
