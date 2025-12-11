The 4O3A Genius API can produce a number of error codes that provide clues about why a particular command failed. This list contains the list of errors, warnings and information codes that exist in the API.

```
#define CLIENT_ERROR_BASE       0x00                        // OK
    
#define CLIENT_ERROR_FORMAT     CLIENT_ERROR_BASE + 0x001   // Invalid command format
#define CLIENT_ERROR_UNKNOWN    CLIENT_ERROR_BASE + 0x010   // Unknown command
#define CLIENT_ERROR_COMMAND    CLIENT_ERROR_BASE + 0x020   // Invalid command parameters
#define CLIENT_ERROR_SUBOBJ     CLIENT_ERROR_BASE + 0x030   // Invalid subscription object
#define CLIENT_ERROR_AUTH       CLIENT_ERROR_BASE + 0x0FF   // Client not authorized
```