### GET
Gets the device firmware, hardware and capability information
```
C<seq_number>|info get
```
Example:
```
C54|info get
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R54|0|info v=4.0.22 date=2023-08-22 btl=1.6 hw=2.0 serial=9A-3A-DC name=Antenna_Genius ports=2 antennas=8 mode=master uptime=3600
```
| Parameter | Value|
|---|---|
|v|Firmware version installed|
|date|Firmware build date|
|btl|Bootloader version|
|hw|Device hardware version|
|serial|Unique serial number generated based on network adapter MAC address|
|name|User-defined network name|
|ports|Number of radio ports available|
|antennas|Number of antenna ports available|
|mode|AG stack operating mode [master or slave]|
|uptime|Number of seconds since the last power-on|