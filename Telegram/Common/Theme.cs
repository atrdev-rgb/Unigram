//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Td.Api;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using AcrylicBrush = Microsoft.UI.Xaml.Media.AcrylicBrush;

namespace Telegram.Common
{
    public class Theme : ResourceDictionary
    {
        [ThreadStatic]
        public static Theme Current;

        private readonly ApplicationDataContainer _isolatedStore;
        private readonly bool _isPrimary;

        public Theme()
        {
            _isPrimary = Current == null;

            try
            {
                _isolatedStore = ApplicationData.Current.LocalSettings.CreateContainer("Theme", ApplicationDataCreateDisposition.Always);
                Current ??= this;

                this.Add("MessageFontSize", GetValueOrDefault("MessageFontSize", 14d));

                var emojiSet = SettingsService.Current.Appearance.EmojiSet;
                switch (emojiSet.Id)
                {
                    case "microsoft":
                        this.Add("EmojiThemeFontFamily", new FontFamily($"XamlAutoFontFamily"));
                        break;
                    case "apple":
                        this.Add("EmojiThemeFontFamily", new FontFamily($"ms-appx:///Assets/Emoji/{emojiSet.Id}.ttf#Segoe UI Emoji"));
                        break;
                    default:
                        this.Add("EmojiThemeFontFamily", new FontFamily($"ms-appdata:///local/emoji/{emojiSet.Id}.{emojiSet.Version}.ttf#Segoe UI Emoji"));
                        break;
                }

                this.Add("ThreadStackLayout", new StackLayout());
            }
            catch { }

            if (_isPrimary)
            {
                Update(ApplicationTheme.Light);
                Update(ApplicationTheme.Dark);
            }
        }

        public static Color Accent { get; private set; } = Colors.Red;

        public ChatTheme ChatTheme => _lastTheme;

        public void Update(ElementTheme requested)
        {
            Update(requested == ElementTheme.Light
                ? ApplicationTheme.Light
                : ApplicationTheme.Dark);
        }

        public ThemeParameters Parameters { get; private set; }

        #region Local 

        private int? _lastAccent;
        private long? _lastBackground;

        private ChatTheme _lastTheme;

        public bool Update(ElementTheme elementTheme, ChatTheme theme)
        {
            var updated = false;
            var requested = elementTheme == ElementTheme.Dark ? TelegramTheme.Dark : TelegramTheme.Light;

            var settings = requested == TelegramTheme.Light ? theme?.LightSettings : theme?.DarkSettings;
            if (settings != null)
            {
                if (_lastAccent != settings.AccentColor)
                {
                    _lastTheme = theme;

                    var tint = SettingsService.Current.Appearance[requested].Type;
                    if (tint == TelegramThemeType.Classic || (tint == TelegramThemeType.Custom && requested == TelegramTheme.Light))
                    {
                        tint = TelegramThemeType.Day;
                    }
                    else if (tint == TelegramThemeType.Custom)
                    {
                        tint = TelegramThemeType.Tinted;
                    }

                    var accent = settings.AccentColor.ToColor();
                    var outgoing = settings.OutgoingMessageAccentColor.ToColor();

                    var info = ThemeAccentInfo.FromAccent(tint, accent, outgoing);
                    ThemeOutgoing.Update(info.Parent, info.Values);
                    ThemeIncoming.Update(info.Parent, info.Values);
                }
                if (_lastBackground != settings.Background?.Id)
                {
                    updated = true;
                }

                _lastAccent = settings.AccentColor;
                _lastBackground = settings.Background?.Id;
            }
            else
            {
                if (_lastAccent != null)
                {
                    _lastTheme = null;

                    var options = SettingsService.Current.Appearance;
                    if (options[requested].Type == TelegramThemeType.Custom && System.IO.File.Exists(options[requested].Custom))
                    {
                        var info = ThemeCustomInfo.FromFile(options[requested].Custom);
                        ThemeOutgoing.Update(info.Parent, info.Values);
                        ThemeIncoming.Update(info.Parent, info.Values);
                    }
                    else if (ThemeAccentInfo.IsAccent(options[requested].Type))
                    {
                        var info = ThemeAccentInfo.FromAccent(options[requested].Type, options.Accents[options[requested].Type]);
                        ThemeOutgoing.Update(info.Parent, info.Values);
                        ThemeIncoming.Update(info.Parent, info.Values);
                    }
                    else
                    {
                        ThemeOutgoing.Update(requested);
                        ThemeIncoming.Update(requested);
                    }
                }
                if (_lastBackground != null)
                {
                    updated = true;
                }

                _lastAccent = null;
                _lastBackground = null;
            }

            return updated;
        }

        #endregion

        #region Global

        private void Update(ApplicationTheme theme)
        {
            var settings = SettingsService.Current.Appearance;
            var requested = theme == ApplicationTheme.Light
                ? TelegramTheme.Light
                : TelegramTheme.Dark;

            if (settings.ChatTheme != null)
            {
                Update(requested, settings.ChatTheme);
            }
            else if (settings[requested].Type == TelegramThemeType.Custom && System.IO.File.Exists(settings[requested].Custom))
            {
                Update(ThemeCustomInfo.FromFile(settings[requested].Custom));
            }
            else if (ThemeAccentInfo.IsAccent(settings[requested].Type))
            {
                Update(ThemeAccentInfo.FromAccent(settings[requested].Type, settings.Accents[settings[requested].Type]));
            }
            else
            {
                Update(requested);
            }
        }

        private void Update(TelegramTheme requested, ChatTheme theme)
        {
            var settings = requested == TelegramTheme.Light ? theme?.LightSettings : theme?.DarkSettings;

            var tint = SettingsService.Current.Appearance[requested].Type;
            if (tint == TelegramThemeType.Classic || (tint == TelegramThemeType.Custom && requested == TelegramTheme.Light))
            {
                tint = TelegramThemeType.Day;
            }
            else if (tint == TelegramThemeType.Custom)
            {
                tint = TelegramThemeType.Tinted;
            }

            var accent = settings.AccentColor.ToColor();
            var outgoing = settings.OutgoingMessageAccentColor.ToColor();

            Update(ThemeAccentInfo.FromAccent(tint, accent, outgoing));
        }

        public void Update(string path)
        {
            Update(ThemeCustomInfo.FromFile(path));
        }

        public void Update(ThemeAccentInfo info)
        {
            Update(info.Parent, info.Values, info.Shades);
        }

        private void Update(TelegramTheme requested, IDictionary<string, Color> values = null, IDictionary<AccentShade, Color> shades = null)
        {
            try
            {
                ThemeOutgoing.Update(requested, values);
                ThemeIncoming.Update(requested, values);

                var target = GetOrCreateResources(requested, out bool create);
                var lookup = ThemeService.GetLookup(requested);

                Color GetShade(AccentShade shade)
                {
                    if (shades != null && shades.TryGetValue(shade, out Color accent))
                    {
                        return accent;
                    }
                    else
                    {
                        return ThemeInfoBase.Accents[TelegramThemeType.Day][shade];
                    }
                }

                if (_isPrimary)
                {
                    Accent = GetShade(AccentShade.Default);
                }

                foreach (var item in lookup)
                {
                    if (item.Value is AccentShade or Color)
                    {
                        Color value;
                        if (item.Value is AccentShade shade)
                        {
                            value = GetShade(shade);
                        }
                        else if (values != null && values.TryGetValue(item.Key, out Color themed))
                        {
                            value = themed;
                        }
                        else if (item.Value is Color color)
                        {
                            value = color;
                        }

                        AddOrUpdate<SolidColorBrush>(target, item.Key, create,
                            update => update.Color = value);
                    }
                    else
                    {
                        Color tintColor;
                        double tintOpacity;
                        double? tintLuminosityOpacity;
                        Color fallbackColor;
                        if (item.Value is Acrylic<Color> acrylicColor)
                        {
                            tintColor = acrylicColor.TintColor;
                            tintOpacity = acrylicColor.TintOpacity;
                            tintLuminosityOpacity = acrylicColor.TintLuminosityOpacity;
                            fallbackColor = acrylicColor.FallbackColor;
                        }
                        else if (item.Value is Acrylic<AccentShade> acrylicShade)
                        {
                            tintColor = GetShade(acrylicShade.TintColor);
                            tintOpacity = acrylicShade.TintOpacity;
                            tintLuminosityOpacity = acrylicShade.TintLuminosityOpacity;
                            fallbackColor = GetShade(acrylicShade.FallbackColor);
                        }
                        else
                        {
                            continue;
                        }

                        AddOrUpdate<AcrylicBrush>(target, item.Key, create, update =>
                        {
                            update.TintColor = tintColor;
                            update.TintOpacity = tintOpacity;
                            update.TintLuminosityOpacity = tintLuminosityOpacity;
                            update.FallbackColor = fallbackColor;
                            update.AlwaysUseFallback = !PowerSavingPolicy.AreMaterialsEnabled;
                        });
                    }
                }

                if (create)
                {
                    ThemeDictionaries.Add(requested == TelegramTheme.Light ? "Light" : "Dark", target);
                }

                int GetColor(string key)
                {
                    if (target.TryGet(key, out SolidColorBrush brush))
                    {
                        return brush.Color.ToValue();
                    }

                    return 0;
                }

                Parameters = new ThemeParameters
                {
                    BackgroundColor = GetColor("ContentDialogBackground"),
                    TextColor = GetColor("ContentDialogForeground"),
                    ButtonColor = GetColor("ButtonBackground"),
                    ButtonTextColor = GetColor("ButtonForeground"),
                    HintColor = GetColor("SystemControlDisabledChromeDisabledLowBrush"),
                    LinkColor = GetColor("HyperlinkForeground")
                };
            }
            catch (UnauthorizedAccessException)
            {
                // Some times access denied is thrown,
                // this seems to happen after the application
                // is resumed, but unfortunately I can't see
                // any fix to this. The exception is going
                // to be thrown any time - even minutes after 
                // the resume - if the theme changes.

                // The exception MIGHT be related to StaticResources
                // but I'm not able to confirm this.
            }
        }

        private void AddOrUpdate<T>(ResourceDictionary target, string key, bool create, Action<T> callback) where T : new()
        {
            if (create)
            {
                var value = new T();
                callback(value);
                target[key] = value;
            }
            else if (target.TryGet(key, out T update))
            {
                callback(update);
            }
        }

        private ResourceDictionary GetOrCreateResources(TelegramTheme requested, out bool create)
        {
            if (ThemeDictionaries.TryGet(requested == TelegramTheme.Light ? "Light" : "Dark", out ResourceDictionary target))
            {
                create = false;
            }
            else
            {
                create = true;
                target = new ResourceDictionary();
            }

            return target;
        }

        #endregion

        #region Settings

        private int? _messageFontSize;
        public int MessageFontSize
        {
            get
            {
                if (_messageFontSize == null)
                {
                    _messageFontSize = (int)GetValueOrDefault("MessageFontSize", 14d);
                }

                return _messageFontSize ?? 14;
            }
            set
            {
                _messageFontSize = value;
                AddOrUpdateValue("MessageFontSize", (double)value);
            }
        }

        public bool AddOrUpdateValue(string key, object value)
        {
            bool valueChanged = false;

            if (_isolatedStore.Values.ContainsKey(key))
            {
                if (_isolatedStore.Values[key] != value)
                {
                    _isolatedStore.Values[key] = value;
                    valueChanged = true;
                }
            }
            else
            {
                _isolatedStore.Values.Add(key, value);
                valueChanged = true;
            }

            if (valueChanged)
            {
                try
                {
                    if (this.ContainsKey(key))
                    {
                        this[key] = value;
                    }
                    else
                    {
                        this.Add(key, value);
                    }
                }
                catch { }
            }

            return valueChanged;
        }

        public valueType GetValueOrDefault<valueType>(string key, valueType defaultValue)
        {
            valueType value;

            if (_isolatedStore.Values.ContainsKey(key))
            {
                value = (valueType)_isolatedStore.Values[key];
            }
            else
            {
                value = defaultValue;
            }

            return value;
        }

        #endregion
    }

    public class ThemeOutgoing : ResourceDictionary
    {
        [ThreadStatic]
        private static Dictionary<string, (Color Color, SolidColorBrush Brush)> _light;
        public static Dictionary<string, (Color Color, SolidColorBrush Brush)> Light => _light ??= new()
        {
            { "MessageForegroundBrush", (Color.FromArgb(0xFF, 0x00, 0x00, 0x00), new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00))) },
            { "MessageForegroundLinkBrush", (Color.FromArgb(0xFF, 0x16, 0x8A, 0xCD), new SolidColorBrush(Color.FromArgb(0xFF, 0x16, 0x8A, 0xCD))) },
            { "MessageBackgroundBrush", (Color.FromArgb(0xFF, 0xF0, 0xFD, 0xDF), new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xFD, 0xDF))) },
            { "MessageSubtleLabelBrush", (Color.FromArgb(0xFF, 0x6D, 0xC2, 0x64), new SolidColorBrush(Color.FromArgb(0xFF, 0x6D, 0xC2, 0x64))) },
            { "MessageSubtleGlyphBrush", (Color.FromArgb(0xFF, 0x5D, 0xC4, 0x52), new SolidColorBrush(Color.FromArgb(0xFF, 0x5D, 0xC4, 0x52))) },
            { "MessageSubtleForegroundBrush", (Color.FromArgb(0xFF, 0x6D, 0xC2, 0x64), new SolidColorBrush(Color.FromArgb(0xFF, 0x6D, 0xC2, 0x64))) },
            { "MessageHeaderForegroundBrush", (Color.FromArgb(0xFF, 0x3A, 0x8E, 0x26), new SolidColorBrush(Color.FromArgb(0xFF, 0x3A, 0x8E, 0x26))) },
            { "MessageHeaderBorderBrush", (Color.FromArgb(0xFF, 0x5D, 0xC4, 0x52), new SolidColorBrush(Color.FromArgb(0xFF, 0x5D, 0xC4, 0x52))) },
            { "MessageMediaForegroundBrush", (Color.FromArgb(0xFF, 0xF0, 0xFD, 0xDF), new SolidColorBrush(Color.FromArgb(0xFF, 0xF0, 0xFD, 0xDF))) },
            { "MessageMediaBackgroundBrush", (Color.FromArgb(0xFF, 0x78, 0xC6, 0x7F), new SolidColorBrush(Color.FromArgb(0xFF, 0x78, 0xC6, 0x7F))) },
            { "MessageOverlayBackgroundBrush", (Color.FromArgb(0x54, 0x00, 0x00, 0x00), new SolidColorBrush(Color.FromArgb(0x54, 0x00, 0x00, 0x00))) },
            { "MessageCallForegroundBrush", (Color.FromArgb(0xFF, 0x2A, 0xB3, 0x2A), new SolidColorBrush(Color.FromArgb(0xFF, 0x2A, 0xB3, 0x2A))) },
            { "MessageCallMissedForegroundBrush", (Color.FromArgb(0xFF, 0xDD, 0x58, 0x49), new SolidColorBrush(Color.FromArgb(0xFF, 0xDD, 0x58, 0x49))) },
            { "MessageReactionBackgroundBrush", (Color.FromArgb(0xFF, 0xD5, 0xF1, 0xC9), new SolidColorBrush(Color.FromArgb(0xFF, 0xD5, 0xF1, 0xC9))) },
            { "MessageReactionForegroundBrush", (Color.FromArgb(0xFF, 0x45, 0xA3, 0x2D), new SolidColorBrush(Color.FromArgb(0xFF, 0x45, 0xA3, 0x2D))) },
            { "MessageReactionChosenBackgroundBrush", (Color.FromArgb(0xFF, 0x5F, 0xBE, 0x67), new SolidColorBrush(Color.FromArgb(0xFF, 0x5F, 0xBE, 0x67))) },
            { "MessageReactionChosenForegroundBrush", (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))) },
        };

        [ThreadStatic]
        private static Dictionary<string, (Color Color, SolidColorBrush Brush)> _dark;
        public static Dictionary<string, (Color Color, SolidColorBrush Brush)> Dark => _dark ??= new()
        {
            { "MessageForegroundBrush", (Color.FromArgb(0xFF, 0xE4, 0xEC, 0xF2), new SolidColorBrush(Color.FromArgb(0xFF, 0xE4, 0xEC, 0xF2))) },
            { "MessageForegroundLinkBrush", (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))) },
            { "MessageBackgroundBrush", (Color.FromArgb(0xFF, 0x2B, 0x52, 0x78), new SolidColorBrush(Color.FromArgb(0xFF, 0x2B, 0x52, 0x78))) },
            { "MessageSubtleLabelBrush", (Color.FromArgb(0xFF, 0x7D, 0xA8, 0xD3), new SolidColorBrush(Color.FromArgb(0xFF, 0x7D, 0xA8, 0xD3))) },
            { "MessageSubtleGlyphBrush", (Color.FromArgb(0xFF, 0x72, 0xBC, 0xFD), new SolidColorBrush(Color.FromArgb(0xFF, 0x72, 0xBC, 0xFD))) },
            { "MessageSubtleForegroundBrush", (Color.FromArgb(0xFF, 0x7D, 0xA8, 0xD3), new SolidColorBrush(Color.FromArgb(0xFF, 0x7D, 0xA8, 0xD3))) },
            { "MessageHeaderForegroundBrush", (Color.FromArgb(0xFF, 0x90, 0xCA, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0x90, 0xCA, 0xFF))) },
            { "MessageHeaderBorderBrush", (Color.FromArgb(0xFF, 0x65, 0xB9, 0xF4), new SolidColorBrush(Color.FromArgb(0xFF, 0x65, 0xB9, 0xF4))) },
            { "MessageMediaForegroundBrush", (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))) },
            { "MessageMediaBackgroundBrush", (Color.FromArgb(0xFF, 0x4C, 0x9C, 0xE2), new SolidColorBrush(Color.FromArgb(0xFF, 0x4C, 0x9C, 0xE2))) },
            { "MessageOverlayBackgroundBrush", (Color.FromArgb(0x54, 0x00, 0x00, 0x00), new SolidColorBrush(Color.FromArgb(0x54, 0x00, 0x00, 0x00))) },
            { "MessageCallForegroundBrush", (Color.FromArgb(0xFF, 0x49, 0xA2, 0xF0), new SolidColorBrush(Color.FromArgb(0xFF, 0x49, 0xA2, 0xF0))) },
            { "MessageCallMissedForegroundBrush", (Color.FromArgb(0xFF, 0xED, 0x50, 0x50), new SolidColorBrush(Color.FromArgb(0xFF, 0xED, 0x50, 0x50))) },
            { "MessageReactionBackgroundBrush", (Color.FromArgb(0xFF, 0x2B, 0x41, 0x53), new SolidColorBrush(Color.FromArgb(0xFF, 0x2B, 0x41, 0x53))) },
            { "MessageReactionForegroundBrush", (Color.FromArgb(0xFF, 0x7A, 0xC3, 0xF4), new SolidColorBrush(Color.FromArgb(0xFF, 0x7A, 0xC3, 0xF4))) },
            { "MessageReactionChosenBackgroundBrush", (Color.FromArgb(0xFF, 0x31, 0x8E, 0xE4), new SolidColorBrush(Color.FromArgb(0xFF, 0x31, 0x8E, 0xE4))) },
            { "MessageReactionChosenForegroundBrush", (Color.FromArgb(0xFF, 0x33, 0x39, 0x3F), new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0x39, 0x3F))) },
        };

        public ThemeOutgoing()
        {
            var light = new ResourceDictionary();
            var dark = new ResourceDictionary();

            foreach (var item in Light)
            {
                light[item.Key] = item.Value.Brush;
            }

            foreach (var item in Dark)
            {
                dark[item.Key] = item.Value.Brush;
            }

            ThemeDictionaries["Light"] = light;
            ThemeDictionaries["Default"] = dark;
        }

        public static void Update(TelegramTheme parent, IDictionary<string, Color> values = null)
        {
            if (values == null)
            {
                Update(parent);
                return;
            }

            var target = parent == TelegramTheme.Dark ? Dark : Light;

            foreach (var value in target)
            {
                var key = value.Key.Substring(0, value.Key.Length - "Brush".Length);
                if (values.TryGetValue($"{key}Outgoing", out Color color))
                {
                    value.Value.Brush.Color = color;
                }
                else
                {
                    value.Value.Brush.Color = value.Value.Color;
                }
            }
        }

        public static void Update(TelegramTheme parent)
        {
            if (parent == TelegramTheme.Light)
            {
                foreach (var value in Light)
                {
                    value.Value.Brush.Color = value.Value.Color;
                }
            }
            else
            {
                foreach (var value in Dark)
                {
                    value.Value.Brush.Color = value.Value.Color;
                }
            }
        }
    }

    public class ThemeIncoming : ResourceDictionary
    {
        [ThreadStatic]
        private static Dictionary<string, (Color Color, SolidColorBrush Brush)> _light;
        public static Dictionary<string, (Color Color, SolidColorBrush Brush)> Light => _light ??= new()
        {
            { "MessageForegroundBrush", (Color.FromArgb(0xFF, 0x00, 0x00, 0x00), new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x00, 0x00))) },
            { "MessageForegroundLinkBrush", (Color.FromArgb(0xFF, 0x16, 0x8A, 0xCD), new SolidColorBrush(Color.FromArgb(0xFF, 0x16, 0x8A, 0xCD))) },
            { "MessageBackgroundBrush", (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))) },
            { "MessageSubtleLabelBrush", (Color.FromArgb(0xFF, 0xA1, 0xAD, 0xB6), new SolidColorBrush(Color.FromArgb(0xFF, 0xA1, 0xAD, 0xB6))) },
            { "MessageSubtleGlyphBrush", (Color.FromArgb(0xFF, 0xA1, 0xAD, 0xB6), new SolidColorBrush(Color.FromArgb(0xFF, 0xA1, 0xAD, 0xB6))) },
            { "MessageSubtleForegroundBrush", (Color.FromArgb(0xFF, 0xA1, 0xAD, 0xB6), new SolidColorBrush(Color.FromArgb(0xFF, 0xA1, 0xAD, 0xB6))) },
            { "MessageHeaderForegroundBrush", (Color.FromArgb(0xFF, 0x15, 0x8D, 0xCD), new SolidColorBrush(Color.FromArgb(0xFF, 0x15, 0x8D, 0xCD))) },
            { "MessageHeaderBorderBrush", (Color.FromArgb(0xFF, 0x37, 0xA4, 0xDE), new SolidColorBrush(Color.FromArgb(0xFF, 0x37, 0xA4, 0xDE))) },
            { "MessageMediaForegroundBrush", (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))) },
            { "MessageMediaBackgroundBrush", (Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3), new SolidColorBrush(Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3))) },
            { "MessageOverlayBackgroundBrush", (Color.FromArgb(0x54, 0x00, 0x00, 0x00), new SolidColorBrush(Color.FromArgb(0x54, 0x00, 0x00, 0x00))) },
            { "MessageCallForegroundBrush", (Color.FromArgb(0xFF, 0x2A, 0xB3, 0x2A), new SolidColorBrush(Color.FromArgb(0xFF, 0x2A, 0xB3, 0x2A))) },
            { "MessageCallMissedForegroundBrush", (Color.FromArgb(0xFF, 0xDD, 0x58, 0x49), new SolidColorBrush(Color.FromArgb(0xFF, 0xDD, 0x58, 0x49))) },
            { "MessageReactionBackgroundBrush", (Color.FromArgb(0xFF, 0xE8, 0xF5, 0xFC), new SolidColorBrush(Color.FromArgb(0xFF, 0xE8, 0xF5, 0xFC))) },
            { "MessageReactionForegroundBrush", (Color.FromArgb(0xFF, 0x16, 0x8D, 0xCD), new SolidColorBrush(Color.FromArgb(0xFF, 0x16, 0x8D, 0xCD))) },
            { "MessageReactionChosenBackgroundBrush", (Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3), new SolidColorBrush(Color.FromArgb(0xFF, 0x40, 0xA7, 0xE3))) },
            { "MessageReactionChosenForegroundBrush", (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))) },
        };

        [ThreadStatic]
        private static Dictionary<string, (Color Color, SolidColorBrush Brush)> _dark;
        public static Dictionary<string, (Color Color, SolidColorBrush Brush)> Dark => _dark ??= new()
        {
            { "MessageForegroundBrush", (Color.FromArgb(0xFF, 0xF5, 0xF5, 0xF5), new SolidColorBrush(Color.FromArgb(0xFF, 0xF5, 0xF5, 0xF5))) },
            { "MessageForegroundLinkBrush", (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))) },
            { "MessageBackgroundBrush", (Color.FromArgb(0xFF, 0x18, 0x25, 0x33), new SolidColorBrush(Color.FromArgb(0xFF, 0x18, 0x25, 0x33))) },
            { "MessageSubtleLabelBrush", (Color.FromArgb(0xFF, 0x6D, 0x7F, 0x8F), new SolidColorBrush(Color.FromArgb(0xFF, 0x6D, 0x7F, 0x8F))) },
            { "MessageSubtleGlyphBrush", (Color.FromArgb(0xFF, 0x6D, 0x7F, 0x8F), new SolidColorBrush(Color.FromArgb(0xFF, 0x6D, 0x7F, 0x8F))) },
            { "MessageSubtleForegroundBrush", (Color.FromArgb(0xFF, 0x6D, 0x7F, 0x8F), new SolidColorBrush(Color.FromArgb(0xFF, 0x6D, 0x7F, 0x8F))) },
            { "MessageHeaderForegroundBrush", (Color.FromArgb(0xFF, 0x71, 0xBA, 0xFA), new SolidColorBrush(Color.FromArgb(0xFF, 0x71, 0xBA, 0xFA))) },
            { "MessageHeaderBorderBrush", (Color.FromArgb(0xFF, 0x42, 0x9B, 0xDB), new SolidColorBrush(Color.FromArgb(0xFF, 0x42, 0x9B, 0xDB))) },
            { "MessageMediaForegroundBrush", (Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF), new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF))) },
            { "MessageMediaBackgroundBrush", (Color.FromArgb(0xFF, 0x3F, 0x96, 0xD0), new SolidColorBrush(Color.FromArgb(0xFF, 0x3F, 0x96, 0xD0))) },
            { "MessageOverlayBackgroundBrush", (Color.FromArgb(0x54, 0x00, 0x00, 0x00), new SolidColorBrush(Color.FromArgb(0x54, 0x00, 0x00, 0x00))) },
            { "MessageCallForegroundBrush", (Color.FromArgb(0xFF, 0x49, 0xA2, 0xF0), new SolidColorBrush(Color.FromArgb(0xFF, 0x49, 0xA2, 0xF0))) },
            { "MessageCallMissedForegroundBrush", (Color.FromArgb(0xFF, 0xED, 0x50, 0x50), new SolidColorBrush(Color.FromArgb(0xFF, 0xED, 0x50, 0x50))) },
            { "MessageReactionBackgroundBrush", (Color.FromArgb(0xFF, 0x3A, 0x47, 0x54), new SolidColorBrush(Color.FromArgb(0xFF, 0x3A, 0x47, 0x54))) },
            { "MessageReactionForegroundBrush", (Color.FromArgb(0xFF, 0x67, 0xBB, 0xF3), new SolidColorBrush(Color.FromArgb(0xFF, 0x67, 0xBB, 0xF3))) },
            { "MessageReactionChosenBackgroundBrush", (Color.FromArgb(0xFF, 0x6E, 0xB2, 0xEE), new SolidColorBrush(Color.FromArgb(0xFF, 0x6E, 0xB2, 0xEE))) },
            { "MessageReactionChosenForegroundBrush", (Color.FromArgb(0xFF, 0x33, 0x39, 0x3F), new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0x39, 0x3F))) },
        };

        public ThemeIncoming()
        {
            var light = new ResourceDictionary();
            var dark = new ResourceDictionary();

            foreach (var item in Light)
            {
                light[item.Key] = item.Value.Brush;
            }

            foreach (var item in Dark)
            {
                dark[item.Key] = item.Value.Brush;
            }

            ThemeDictionaries["Light"] = light;
            ThemeDictionaries["Default"] = dark;
        }

        public static void Update(TelegramTheme parent, IDictionary<string, Color> values = null)
        {
            if (values == null)
            {
                Update(parent);
                return;
            }

            var target = parent == TelegramTheme.Dark ? Dark : Light;

            foreach (var value in target)
            {
                var key = value.Key.Substring(0, value.Key.Length - "Brush".Length);
                if (values.TryGetValue($"{key}Incoming", out Color color))
                {
                    value.Value.Brush.Color = color;
                }
                else
                {
                    value.Value.Brush.Color = value.Value.Color;
                }
            }
        }

        public static void Update(TelegramTheme parent)
        {
            if (parent == TelegramTheme.Light)
            {
                foreach (var value in Light)
                {
                    value.Value.Brush.Color = value.Value.Color;
                }
            }
            else
            {
                foreach (var value in Dark)
                {
                    value.Value.Brush.Color = value.Value.Color;
                }
            }
        }
    }
}