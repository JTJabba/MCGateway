using JTJabba.EasyConfig;
using JTJabba.EasyConfig.Loader;
using System.Collections.Immutable;

namespace MCGateway
{
    public static class Translation
    {
        /// <summary>
        /// Dictionary of languages by name to their respective translation objects
        /// </summary>
        public static ImmutableSortedDictionary<string, Config.TranslationsObject> Translations { get; private set; }
        public static Config.TranslationsObject DefaultTranslation { get; private set; }

        static void LoadTranslations()
        {
            {
                var temp = new Dictionary<string, Config.TranslationsObject>();
                foreach (var translation in Config.Translations)
                    temp.Add(translation.Language, translation);
                Translations = temp.ToImmutableSortedDictionary();
            }
            {
                Translations.TryGetValue(Config.DefaultLanguage, out var translation);
                if (translation == null)
                    throw new KeyNotFoundException("No translation defined for default language");
                DefaultTranslation = translation;
            }
        }

#pragma warning disable CS8618 // We can assume config is loaded before any calls to Translation
        static Translation()
#pragma warning restore CS8618
        {
            ConfigLoader.AddOnFirstStaticLoadCallback(LoadTranslations);
        }
    }
}
