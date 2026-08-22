using System.Runtime.CompilerServices;

// The generator and custom inspectors write straight into the bank's serialized
// arrays. Keeping those members internal stops game code from mutating generated
// data by accident while still letting the editor tooling do its job.
[assembly: InternalsVisibleTo("Essential.Audio.Editor")]
