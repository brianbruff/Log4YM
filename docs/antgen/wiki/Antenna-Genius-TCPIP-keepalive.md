### KEEPALIVE
Authorizes the client to control the device if the connection is established from WAN
```
C<seq_number>|keepalive enable|disable
```
Example:
```
C19|keepalive enable
```
See [Response Format](https://github.com/4o3a/genius-api-docs/wiki/Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the radio
| Hex Response | Meaning |
|---|---|
|0x00|OK authorized|
|0x20|Invalid command parameters|

Response Example:
```
R19|0|
```

With the TCP Channel Keep-alive mechanism enabled, the device expects to receive a ping command from the client once per second. If 5 seconds elapse without a [ping command](Antenna-Genius-TCPIP-ping), the client will be disconnected and the socket will be closed.














