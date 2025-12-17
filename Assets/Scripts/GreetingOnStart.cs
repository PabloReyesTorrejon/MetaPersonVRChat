// GreetingOnStart was causing duplicated greetings when SceneGreetingListener is also present.
// Prefer using `SceneGreetingListener` which auto-detects the `VoiceChatManager` and
// avoids race conditions. This file is kept as an inert placeholder; if you prefer
// to delete it entirely, remove `GreetingOnStart.cs` from the project.

// If you have scene GameObjects that reference GreetingOnStart, replace them with
// a SceneGreetingListener or ensure only one of the two scripts is present.
