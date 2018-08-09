using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace WearSync.Shared
{
    public class AccentColors
    {
        private AccentColor Id { get; set; }
        private string Title { get; set; }
        private string Resource { get; set; }

        private AccentColors(AccentColor id, string name, string resource)
        {
            Id = id;
            Title = name;
            Resource = resource;
        }

        static readonly List<AccentColors> AccentColorsList = new List<AccentColors>()
        {
            new AccentColors(AccentColor.Red, "Red", "accent_red"),
            new AccentColors(AccentColor.Orange, "Orange", "accent_orange"),
            new AccentColors(AccentColor.Yellow, "Yellow", "accent_yellow"),
            new AccentColors(AccentColor.Green, "Green", "accent_green"),
            new AccentColors(AccentColor.Blue, "Blue", "accent_blue"),
            new AccentColors(AccentColor.Purple, "Purple", "accent_purple")
        };

        #region Public Methods

        public static AccentColor GetIdFromResource(string resource) => AccentColorsList.FirstOrDefault(x => x.Resource == resource).Id;

        public static string GetResource(AccentColor color) => AccentColorsList.FirstOrDefault(x => x.Id == color)?.Resource;

        public static string GetTitle(AccentColor color) => AccentColorsList.FirstOrDefault(x => x.Id == color)?.Title;

        public static List<AccentColor> GetAccentColors() => (Enum.GetValues(typeof(AccentColor)) as AccentColor[]).ToList();

        public static AccentColor GetNextColor(AccentColor currentColor)
        {
            List<AccentColor> accentColors = GetAccentColors();

            var total = accentColors.Count;
            var currentIndex = accentColors.IndexOf(currentColor);

            var nextIndex = currentIndex == total - 1 ? 0 : currentIndex + 1;

            return accentColors[nextIndex];
        }

        #endregion
    }

    public enum AccentColor
    {
        Red,
        Orange,
        Yellow,
        Green,
        Blue,
        Purple
    }
}