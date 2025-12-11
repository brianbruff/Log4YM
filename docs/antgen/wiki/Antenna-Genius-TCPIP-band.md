### LIST
This command is used to list the available band slots (0-15) on the device, based on which it detects the correct band by comparing the frequency retrieved from any reliable frequency source (FLEX, LAN, etc.)
```
C<seq_number>|band list
```
Example:
```
C9|band list
```

The command will return the band configuration table in multiple responses with the same <seq_number>, containing a message with band configuration, prior to returning the OK status.

See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R9|0|band 0 name=None freq_start=0.000000 freq_stop=0.000000
R9|0|band 1 name=160m freq_start=1.600000 freq_stop=2.200000
R9|0|band 2 name=80m freq_start=3.300000 freq_stop=4.000000
R9|0|band 3 name=40m freq_start=6.800000 freq_stop=7.400000
R9|0|band 4 name=30m freq_start=9.900000 freq_stop=10.350000
R9|0|band 5 name=20m freq_start=13.800000 freq_stop=14.550000
R9|0|band 6 name=17m freq_start=17.868000 freq_stop=18.368000
R9|0|band 7 name=15m freq_start=20.800000 freq_stop=21.650000
R9|0|band 8 name=12m freq_start=24.690000 freq_stop=25.190000
R9|0|band 9 name=10m freq_start=27.800000 freq_stop=29.900000
R9|0|band 10 name=6m freq_start=49.800000 freq_stop=52.200000
R9|0|band 11 name=60m freq_start=5.000000 freq_stop=6.000000
R9|0|band 12 name=Custom_1 freq_start=0.000000 freq_stop=0.000000
R9|0|band 13 name=Custom_2 freq_start=0.000000 freq_stop=0.000000
R9|0|band 14 name=Custom_3 freq_start=0.000000 freq_stop=0.000000
R9|0|band 15 name=Custom_4 freq_start=0.000000 freq_stop=0.000000
R9|0|
```
| Parameter | Value|
|---|---|
|id|Band identifier 0 - 15|
|name|Custom band name|
|freq_start|Frequency start|
|freq_stop|Frequency stop|

### SET
All bands are configurable in terms of name, start frequency, and end frequency upon which the device will detect the current band.
```
C<seq_number>|band set <band_id> [name=<name>] [freq_start=<freq_start>] [freq_stop=<freq_stop>]

<band_id>     = band identifier (1-15). Cannot be 0 as it is reserved for a None band / no band data
<name>        = custom band name
<freq_start>  = frequency start
<freq_stop>   = frequency stop
```
Example:
```
C7|band set 12 name=My_special_band freq_start=5.000000 freq_stop=5.200000
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R7|0|
```

### INIT
Initializes band configuration to a factory defaults.
```
C<seq_number>|band init
```
Example:
```
C11|band init
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R11|0|
```