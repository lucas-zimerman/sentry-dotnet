using System;

namespace Sentry.PlatformAbstractions
{
    /// <summary>
    /// Details of the runtime
    /// </summary>
    /// <inheritdoc />
    public class Runtime : IEquatable<Runtime>
    {
        private static Runtime _runtime;
        /// <summary>
        /// Gets the current runtime
        /// </summary>
        /// <value>
        /// The current runtime.
        /// </value>
        public static Runtime Current => _runtime ?? (_runtime = RuntimeInfo.GetRuntime());
        /// <summary>
        /// The name of the runtime
        /// </summary>
        /// <example>
        /// .NET Framework, .NET Native, Mono
        /// </example>
        public string Name { get; internal set; }
        /// <summary>
        /// The version of the runtime
        /// </summary>
        /// <example>
        /// 4.7.2633.0
        /// </example>
        public string Version { get; internal set; }
#if NETFX
        /// <summary>
        /// The .NET Framework installation which is running the process
        /// </summary>
        /// <value>
        /// The framework installation or null if not running .NET Framework
        /// </value>
        public FrameworkInstallation FrameworkInstallation { get; internal set; }
#endif
        /// <summary>
        /// The raw value parsed to extract Name and Version
        /// </summary>
        /// <remarks>
        /// This property will contain a value when the underlying API
        /// returned Name and Version as a single string which required parsing.
        /// </remarks>
        public string Raw { get; internal set; }

#pragma warning disable 1572 // xml doc for frameworkInstallation is conditional
        /// <summary>
        /// Creates a new Runtime instance
        /// </summary>
        /// <param name="name">The name of the runtime</param>
        /// <param name="version">The version of the runtime</param>
        /// <param name="frameworkInstallation">The .NET Framework installation which is running the process</param>
        /// <param name="raw">The raw value when parsing was required</param>
        public Runtime(
            string name = null,
            string version = null,
            #if NETFX
            FrameworkInstallation frameworkInstallation = null,
            #endif
            string raw = null)
        {
            Name = name;
            Version = version;
#if NETFX
            FrameworkInstallation = frameworkInstallation;
#endif
            Raw = raw;
        }
#pragma warning restore 1572

        /// <summary>
        /// The string representation of the Runtime
        /// </summary>
        public override string ToString()
        {
            if (Name == null && Version == null)
            {
                return Raw;
            }

            if (Name != null && Version == null)
            {
                return Raw?.Contains(Name) == true
                    ? Raw
                    : $"{Name} {Raw}";
            }

            return $"{Name} {Version}";
        }

        public bool Equals(Runtime other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Name, other.Name)
                   && string.Equals(Version, other.Version)
#if NETFX
                   && Equals(FrameworkInstallation, other.FrameworkInstallation)
#endif
                   && string.Equals(Raw, other.Raw);
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Runtime) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Version != null ? Version.GetHashCode() : 0);
#if NETFX
                hashCode = (hashCode * 397) ^ (FrameworkInstallation != null ? FrameworkInstallation.GetHashCode() : 0);
#endif
                hashCode = (hashCode * 397) ^ (Raw != null ? Raw.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
