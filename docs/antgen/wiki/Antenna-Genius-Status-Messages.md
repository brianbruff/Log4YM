### ANTENNA
A status message indicating antenna configuration has been changed, which is received by the clients subscribed to antenna changes: [sub antenna](Antenna-Genius-TCPIP-sub#ANTENNA)
```
S0|antenna reload
```
After receiving such a message, the client must reload the antenna configuration using the [antenna list](Antenna-Genius-TCPIP-antenna#LIST) command.

### OUTPUT
A status message indicating output(s) configuration has been changed, which is received by the clients subscribed to output changes: [sub output](Antenna-Genius-TCPIP-sub#OUTPUT)
```
S0|output reload
```
After receiving such a message, the client must reload the output(s) configuration using the [group list](Antenna-Genius-TCPIP-group#LIST) and [output list](Antenna-Genius-TCPIP-output#LIST) commands.
### PORT
A status message indicating radio port status has been changed, which is received by the clients subscribed to port changes: [sub port](Antenna-Genius-TCPIP-sub#PORT)
```
S0|port 1 auto=1 source=AUTO band=0 rxant=0 txant=0 inband=0 tx=0 inhibit=0
S0|port 2 auto=1 source=AUTO band=0 rxant=0 txant=0 inband=0 tx=0 inhibit=0
```
The format of the message is the same as when parsing the [port get](Antenna-Genius-TCPIP-port#GET) command.

### RELAY
A status message indicating the output(s) or relay(s) state has been changed, which is received by the clients subscribed to relay changes: [sub relay](Antenna-Genius-TCPIP-sub#RELAY)
```
S0|relay tx=00 rx=00 state=00
```
| Parameter | Value|
|---|---|
|tx|Reserved for future use|
|rx|Hex representation of selected output(s)|
|state|Hex representation of active relay(s)|