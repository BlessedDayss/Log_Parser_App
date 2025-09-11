using System.Collections.Generic;
using Avalonia.Media;
using Log_Parser_App.Converters.ColorSchemes.Base;

namespace Log_Parser_App.Converters.ColorSchemes.Configurations
{
    public class TabColorSchemeConfiguration : BaseColorSchemeConfiguration
    {
        public const string SCHEME_NAME = "Tab";

        public const string SELECTED_KEY = "SELECTED";
        public const string UNSELECTED_KEY = "UNSELECTED";
        private const string UPDATE_AVAILABLE_KEY = "UPDATE_AVAILABLE";
        private const string DEFAULT_KEY = "DEFAULT";

        public TabColorSchemeConfiguration() : base(SCHEME_NAME)
        {
        }

        protected override IReadOnlyDictionary<string, IBrush> BuildColorDictionary()
        {
            return new Dictionary<string, IBrush>
            {
                [SELECTED_KEY] = CreateBrush("#3A3B3E"),
                [UNSELECTED_KEY] = CreateBrush("#2A2B2D"),
                [UPDATE_AVAILABLE_KEY] = CreateBrush("#4CAF50"),
                [DEFAULT_KEY] = CreateBrush("#AAAAAA")
            };
        }

        protected override IBrush BuildDefaultColor()
        {
            return CreateBrush("#AAAAAA");
        }
    }
}
