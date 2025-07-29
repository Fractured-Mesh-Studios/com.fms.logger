# Unity Enhanced Debug Logging

This repository offers improved logging functionalities for Unity, providing more robust and feature-rich alternatives to Unity's built-in `Debug.Log` methods.

---

## Usage

To use these enhanced logging methods, simply call them from your Unity scripts. They're designed to be a **direct replacement** for the standard `Debug.Log` calls, offering additional benefits like better readability, easier filtering, and the potential for custom handlers.

---

### `DebugClient.Log(object message)`

Use this method for **general information logging**. It works similarly to `Debug.Log()`, but might include extra features such as timestamps, log levels, or custom formatting depending on the underlying implementation.

```csharp
DebugClient.Log("This is a general log message.");
DebugClient.Log(myVariable);
DebugClient.Log(myVariable, gameObject);
````

### `DebugClient.LogWarning(object message)`

Use this method to log warnings that indicate potential issues but don't necessarily stop execution. Warnings often appear differently in the console (e.g., in yellow text) to draw attention.

```csharp
DebugClient.LogWarning("Something unexpected happened, but the application can continue.");
DebugClient.LogWarning(myVariable);
DebugClient.LogWarning(myVariable, gameObject);
````

### `DebugClient.LogError(object message)`

Use this method to log exceptions that have been caught. This method is crucial for debugging unexpected runtime errors, as it typically provides the exception type, message, and a full stack trace, helping you pinpoint the exact location of the error in your code.

```csharp
DebugClient.LogError("Something unexpected happened, but the application can continue.");
DebugClient.LogError(myVariable);
DebugClient.LogError(myVariable, gameObject);
````
