using System.Runtime.CompilerServices;

// The URP bridge reads the runtime's creation hook, which is not public API: game
// code has no reason to attach components to the juice runtime object.
[assembly: InternalsVisibleTo("Essential.Juice.URP")]
