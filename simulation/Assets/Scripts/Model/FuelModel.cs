﻿using System;
using System.Collections.Generic;

[Serializable]
public class FuelModel
{
    public string code;
    public int number;
    public string name;
    public string description;
    public List<float> fuel_load;
    public string type;
    public List<int> sav_ratio;
    public float fuel_bed_depth;
    public float dead_fuel_moisture_of_extinction;
    public int characteristic_sav;
    public float bulk_density;
    public float relative_packing_ratio;
    public const int heat_content = 8000; // constant
    public const float total_mineral_content = 0.0555f; // constant
    public const float effective_mineral_content = 0.01f; // constant
    public float oven_dry_fuel_load => fuel_bed_depth * bulk_density;
    public const float particle_density = 32f; // constant
}

