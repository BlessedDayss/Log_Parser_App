namespace Log_Parser_App.Converters.ColorSchemes
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using Log_Parser_App.Converters.ColorSchemes.Base;
    using Log_Parser_App.Converters.ColorSchemes.Configurations;
    using Log_Parser_App.Converters.Interfaces;


    public class ColorSchemeFactory : IColorSchemeFactory
    {
        private readonly Dictionary<string, IColorSchemeConfiguration> _registeredSchemes;
        private readonly Lock _lock = new Lock();

        public ColorSchemeFactory() {
            _registeredSchemes = new Dictionary<string, IColorSchemeConfiguration>(StringComparer.OrdinalIgnoreCase);
            RegisterDefaultSchemes();
        }

        public IColorProvider CreateColorProvider(string schemeName) {
            if (string.IsNullOrWhiteSpace(schemeName))
                throw new ArgumentException("Scheme name cannot be null or empty", nameof(schemeName));

            lock (_lock) {
                if (!_registeredSchemes.TryGetValue(schemeName, out var configuration)) {
                    throw new ArgumentException($"Color scheme '{schemeName}' is not registered", nameof(schemeName));
                }

                return new BaseColorProvider(configuration);
            }
        }

        private void RegisterScheme(IColorSchemeConfiguration configuration) {
            ArgumentNullException.ThrowIfNull(configuration);

            if (string.IsNullOrWhiteSpace(configuration.SchemeName))
                throw new ArgumentException("Configuration must have a valid scheme name");

            lock (_lock) {
                _registeredSchemes[configuration.SchemeName] = configuration;
            }
        }

        private void RegisterDefaultSchemes() {
            RegisterScheme(new LogLevelColorSchemeConfiguration());
            RegisterScheme(new TabColorSchemeConfiguration());
        }

    }
}