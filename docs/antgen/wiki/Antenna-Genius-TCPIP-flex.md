### LIST
This command is used to list the available FlexRadio devices connected to a LAN.

```
C<seq_number>|flex list
```
Example:
```
C77|flex list
```

The command will return the list of discovered radios in multiple responses with the same <seq_number>, containing a message with radio information prior to returning the OK status.

See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R77|0|flex model=FLEX-6700 serial=1514-3143-6700-7544 nickname=Ranko callsign=4O3A
R77|0|
```
| Parameter | Value|
|---|---|
|model|FlexRadio model|
|serial|Serial number - used to bind to the AG device|
|nickname|Radio nickname|
|callsign|Configured callsign|

### GET
Gets the per-port (A and B) integration configuration parameters
```
C<seq_number>|flex get <port_id>

<port_id>   = Port identifier / 1=A, 2=B
```
Example:
```
C12|flex get 1
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R12|0|flex 1 serial= ant=ANT2 ptt=LAN
```
| Parameter | Value|
|---|---|
|id|Port identifier|
|serial|Configured serial number|
|ant|Port connected to antenna port|
|ptt|PTT source - LAN or RCA|

### SET
Configuring AG-FlexRadio port integration
```
C<seq_number>|flex set <port_id> serial=<serial_no> ant=<antenna_port> ptt=<ptt_source>

<port_id>        = Port identifier
<serial_no>      = Radio serial number for binding
<antenna_port>   = AG port is connected to: ANT1, ANT2, XVRT
<ptt_source>     = PTT source: LAN or RCA
```
Example:
```
C8|flex set 1 serial=1514-3143-6700-7544 ant=ANT1 ptt=LAN
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R8|0|
```