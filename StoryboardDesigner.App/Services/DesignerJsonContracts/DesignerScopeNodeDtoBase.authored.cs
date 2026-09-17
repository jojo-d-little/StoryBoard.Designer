using System.Text.Json.Serialization;

namespace StoryboardDesigner.App.Services;

internal abstract partial class DesignerScopeNodeDtoBase
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    string _NameInGame = string.Empty;
	public string NameInGame 
	{ 
		get
		{
			if(!string.IsNullOrWhiteSpace(_NameInGame))
			{
				return _NameInGame;
			}
			return Name;
		}

		set 
		{
			_NameInGame = value;
		}
	}

    public bool HideEmptyConfiguration { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> IgnoredValidationRuleIds { get; set; } = new();
}