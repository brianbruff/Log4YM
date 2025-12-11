### LIST
This command lists the available antennas on the device
```
C<seq_number>|antenna list
```
Example:
```
C5|antenna list
```

The command will return the antenna configuration table in multiple responses with the same <seq_number>, containing a message with the antenna configuration, prior to returning the OK status.

See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R5|0|antenna 1 name=Antenna_1 tx=0000 rx=0001 inband=0000
R5|0|antenna 2 name=Antenna_2 tx=0000 rx=0001 inband=0000
R5|0|antenna 3 name=Antenna_3 tx=0000 rx=0001 inband=0000
R5|0|antenna 4 name=Antenna_4 tx=0000 rx=0001 inband=0000
R5|0|antenna 5 name=Antenna_5 tx=0000 rx=0001 inband=0000
R5|0|antenna 6 name=Antenna_6 tx=0000 rx=0001 inband=0000
R5|0|antenna 7 name=Antenna_7 tx=0000 rx=0001 inband=0000
R5|0|antenna 8 name=Antenna_8 tx=0000 rx=0001 inband=0000
R5|0|
```
| Parameter | Value|
|---|---|
|id|Antenna identifier 1 - xx|
|name|Custom antenna name|
|tx|TX band mask (hex)|
|rx|RX band mask (hex)|
|inband|Inband band mask (hex)|

Parameters like **tx**, **rx**, and **inband** are masks that represent if the antenna is available on a certain band (0-15) where each bit represents a band. 

Example: 0b0000000000000111 masks the antenna to be available on the first 3 configured bands and its hex value is **0x0007**, thus a returned value would be 0007

### SET
This command is used to configure antenna port parameters.
```
C<seq_number>|antenna set <antenna_no> [name=<name>] [tx=<tx>] [rx=<rx>] [inband=<inband>]

<antenna_no>  = antenna identifier (1-xx)
<name>        = custom antenna name
<tx>          = tx band mask
<rx>          = rx band mask
<inband>      = inband band mask
```
Example:
```
C7|antenna set 3 name=My_antenna tx=0007 rx=0007 inband=0000
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R7|0|
```
Whenever the antenna name is changed, each connected client that previously executed [sub antenna](Antenna-Genius-TCPIP-sub#ANTENNA) will receive the [antenna reload](Antenna-Genius-Status-Messages#ANTENNA) update status indicating that the antenna name(s) has changed.