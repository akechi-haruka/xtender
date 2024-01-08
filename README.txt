xtender 1.1.1
2021-2024 Haruka
Licensed under GPLv3.

---

A simple background application that listens to commands sent from a certain game via a named pipe.

Some weird workarounds had to be made to make the game (which runs on a single thread) to not hang.
Commands must always return as soon as possible.
If background work needs to be done (downloads, loading, etc.), call StartDelayed(Action func) and immediately return ERROR_DELAYING.
When the thread completes (or errors), you must set DelayResponse to the response data of the function.

PROTOCOL SPECIFICATION (from xtender.xtal):

A named pipe given by the constructor argument (or default) is opened in r+w mode.
After sending a command, terminated by a newline, we immediately expect a response to not freeze the game.
A response is terminated by the stream closure.
Errors are returned with "error:<detailed error information>".

Should no response be available, xtender must answer with "error:delaying" (ERROR_DELAYING). xtal may retry at any point with the "preq" command. If still no answer exists, "error:delayed" (ERROR_DELAYED) must be answered any number of times until xtender can answer to preq with the orginally intended answer (or another error response).
