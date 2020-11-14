FUEL_MODEL_CLASSIFIED = "40_scott_and_burgan_fire_behaviour_fuel_models_classified.tif"

# Coordinate Reference System's used
WGS84 = "EPSG:4326" # WGS84 latitude-longitude CRS
ALBERS_EQUAL_AREA_CONIC = \
    'PROJCS["USA_Contiguous_Albers_Equal_Area_Conic_USGS_version",' \
        'GEOGCS["NAD83",DATUM["North_American_Datum_1983",' \
            'SPHEROID["GRS 1980",6378137,298.2572221010042, AUTHORITY["EPSG","7019"]],' \
            'AUTHORITY["EPSG","6269"]],' \
            'PRIMEM["Greenwich",0],' \
            'UNIT["degree",0.0174532925199433],' \
            'AUTHORITY["EPSG","4269"]],' \
        'PROJECTION["Albers_Conic_Equal_Area"],' \
        'PARAMETER["standard_parallel_1",29.5],' \
        'PARAMETER["standard_parallel_2",45.5],' \
        'PARAMETER["latitude_of_center",23],' \
        'PARAMETER["longitude_of_center",-96],' \
        'PARAMETER["false_easting",0],' \
        'PARAMETER["false_northing",0],' \
    'UNIT["metre",1,AUTHORITY["EPSG","9001"]]]'