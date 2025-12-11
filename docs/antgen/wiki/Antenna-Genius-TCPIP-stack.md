### LIST
Lists available AG devices on LAN to pair (stack) together.
```
C<seq_number>|stack list
```
Example:
```
C16|stack list
```

The command will return the available AG table in multiple responses with the same <seq_number>, containing a message with the AG information, prior to returning the OK status.

See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the radio

Response example:
```
R16|0|stack serial=C7-F6-5C name=Antenna_Genius antennas=8 ports=2
R16|0|stack serial=92-3A-DC name=Antenna_Genius antennas=8 ports=2
R16|0|
```
| Parameter | Value|
|---|---|
|serial|AG serial number - used for stacking|
|name|Custom set device name|
|antennas|Number of antenna ports on the device|
|ports|Number of radio ports on the device|

### GET
Gets the configured values for AG stacking
```
C<seq_number>|stack get
```
Example:
```
C17|stack get
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the radio

Response Example:
```
R17|0|stack in_use=1 serial=9A-3A-DC mode=1
```
| Parameter | Value|
|---|---|
|in_use|Boolean representing if stacking is enabled|
|serial|AG serial number - used for stacking|
|mode|Stacking mode - 0 = A/B switch, 1 = Using ports 7&8|

### SET
This command is used to configure stack parameters.
```
C<seq_number>|stack set <in_use> [serial=<serial_no>] [mode=<mode>]

<in_use>      = boolean representing if stacking is enabled
<serial>      = AG serial number
<mode>        = Stacking mode - 0 = A/B switch, 1 = Using ports 7&8
```
Example:
```
C7|stack in_use=1 serial=AB-CD-EF mode=1
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R7|0|
```
Whenever the mode is changed, each connected client that previously executed [sub antenna](Antenna-Genius-TCPIP-sub#ANTENNA) will receive the [antenna reload](Antenna-Genius-Status-Messages#ANTENNA) update status indicating that the antenna list has changed.