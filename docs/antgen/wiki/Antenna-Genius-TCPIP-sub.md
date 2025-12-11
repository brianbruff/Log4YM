### Overview
This command will subscribe to messages produced by the device to inform a client of the status of certain objects. Once a subscription has been enabled, status messages will be sent to the client for changes in the object that is the target of the subscription. 

Available objects are:
- [antenna](#ANTENNA)
- [group](#GROUP)
- [output](#OUTPUT)
- [port](#PORT)
- [relay](#RELAY)

In the event that a subscription request is made for an object that does not exist, the following response will be generated:

To unsubscribe from an object, use the [unsub](Antenna-Genius-TCPIP-unsub) command. This will revert the previous subscription command and discontinue status messages about the object.

### PORT
Subscribe to the status of radio ports (A=1, B=2, or both)
```
C<seq_number>|sub port <port_number|all>

<port_number|all>  = port number (A=1, B=2) or all to subscribe to all ports available on the device
```

Example:
```
C21|sub port all
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the radio

Response Example:
```
R21|0|
```
After a successful subscription to an available port, API will start sending asynchronous updates of port changes via [port](Antenna-Genius-Status-Messages#PORT) Status message

### RELAY
Subscribe to the output relay board and output(s) changes
```
C<seq_number>|sub relay
```

Example:
```
C21|sub relay
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the radio

Response Example:
```
R21|0|
```
After a successful subscription to an output relay, API will start sending asynchronous updates of relay changes changes via [relay](Antenna-Genius-Status-Messages#RELAY) Status message