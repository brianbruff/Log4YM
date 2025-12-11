### AUTH
Authorizes the client to control the device if the connection is established from WAN
```
C<seq_number>|auth code=<code>

<code>  = authorization code, previously configured via the 'network' command
```
Example:
```
C54|auth code=123456
```
See [Response Format](Antenna-Genius-TCPIP-API#response-format) for details on the format of the response messages from the device

Response Example:
```
R54|0|  // OK - client authorized
```

```
R54|FF|  // Incorrect authorization code - client not authorized
```
















