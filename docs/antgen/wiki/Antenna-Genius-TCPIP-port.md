### GET
Gets the radio port information
```
C<seq_number>|port get <port_id>

<port_id>   = Radio port on AG / A=1  B=2
```
Example:
```
C15|port get 1
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the radio

Response Example:
```
R15|0|port 1 auto=1 source=AUTO band=0 rxant=0 txant=0 tx=0 inhibit=0
```
| Parameter | Value|
|---|---|
|id|Port identifier 1=A 2=B|
|auto|Autodetect band source|
|source|Band source|
|band|Band identifier 0-15|
|rxant|Selected RX antenna (0 = none)|
|txant|Selected TX antenna (0 = none)|
|tx|Port is in transpission|
|inhibit|Port is inhibited - prevented from transmitting or using current band|

### SET
Setting radio port parameters
```
C<seq_number>|port set <port_id> [auto=<0|1>] [source=<band_source>] [band=<band>] [rxant=<antenna>] [txant=<antenna>]

<port_id>       = Port identifier 1=A 2=B
<auto>          = Autodetect band source
<band_source>   = Band source
<band>          = Band identifier 0-15
<antenna>       = Antenna identifier 0-xx (0 = none)
```

Example:
```
C22|port set 1 auto=0 source=MANUAL band=3 rxant=1 txant=1
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the radio

Response Example:
```
C22|0|
```