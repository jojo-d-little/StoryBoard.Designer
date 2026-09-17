using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using StoryboardDesigner.App.Models;

namespace StoryboardDesigner.App.Views;

public partial class ScopedTokenListDialog : Window
{
    private enum AddTokenResult
    {
        Added,
        Duplicate,
        Empty
    }

    private sealed class DirectionalMappingPreviewItem
    {
        public required string Token { get; init; }
        public required Direction10 TraversalDirection { get; init; }
        public required string DisplayText { get; init; }
    }

    private readonly IList<string> _targetValues;
    private readonly IList<DirectionalTraversalMapping>? _targetDirectionalMappings;
    private readonly ObservableCollection<string> _workingValues;
    private readonly ObservableCollection<string> _inheritedValues;
    private readonly Dictionary<string, Direction10> _workingDirectionalMappings = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _isDirectionalMode;

    public ScopedTokenListDialog(
        string scopeLabel,
        string tokenKind,
        IList<string> values,
        IReadOnlyList<string> inheritedValues,
        IList<DirectionalTraversalMapping>? directionalMappings = null)
    {
        InitializeComponent();

        _targetValues = values;
        _targetDirectionalMappings = directionalMappings;
        _isDirectionalMode = string.Equals(tokenKind, "Directionals", StringComparison.OrdinalIgnoreCase) && directionalMappings is not null;
        _workingValues = new ObservableCollection<string>(values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
        _inheritedValues = new ObservableCollection<string>(inheritedValues
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));

        if (_inheritedValues.Count == 0)
        {
            _inheritedValues.Add("(none)");
        }

        HeaderText.Text = $"{scopeLabel} - {tokenKind}";
        TokensListBox.ItemsSource = _workingValues;
        InheritedTokensListBox.ItemsSource = _inheritedValues;

        DirectionalMappingPanel.Visibility = _isDirectionalMode ? Visibility.Visible : Visibility.Collapsed;
        DirectionalTokenComboBox.ItemsSource = _workingValues;
        TraversalDirectionComboBox.ItemsSource = BuildTraversalDirectionChoices();
        TraversalDirectionComboBox.SelectedIndex = 0;

        if (_isDirectionalMode)
        {
            foreach (var mapping in directionalMappings!)
            {
                var token = mapping.Token?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(token))
                {
                    continue;
                }

                if (_workingValues.Any(existing => string.Equals(existing, token, StringComparison.OrdinalIgnoreCase)))
                {
                    _workingDirectionalMappings[token] = mapping.TraversalDirection;
                }
            }

            RefreshDirectionalMappingsPreview();
        }
    }

    private void AddToken_OnClick(object sender, RoutedEventArgs e)
    {
        var result = TryAddTokenFromInput();
        ShowTokenInputFeedback(result);
    }

    private void TokenInputTextBox_OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        var result = TryAddTokenFromInput();
        ShowTokenInputFeedback(result);
    }

    private void RemoveToken_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.DataContext is not string value)
        {
            return;
        }

        _workingValues.Remove(value);

        if (_isDirectionalMode)
        {
            _workingDirectionalMappings.Remove(value);
            RefreshDirectionalMappingsPreview();
        }
    }

    private void SetDirectionalMapping_OnClick(object sender, RoutedEventArgs e)
    {
        if (!_isDirectionalMode)
        {
            return;
        }

        var token = DirectionalTokenComboBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var selection = TraversalDirectionComboBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selection) || string.Equals(selection, "(unmapped)", StringComparison.OrdinalIgnoreCase))
        {
            _workingDirectionalMappings.Remove(token);
            RefreshDirectionalMappingsPreview();
            return;
        }

        if (!Enum.TryParse<Direction10>(selection, out var traversalDirection))
        {
            return;
        }

        _workingDirectionalMappings[token] = traversalDirection;
        RefreshDirectionalMappingsPreview();
    }

    private void DirectionalMappingsPreviewListBox_OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var hasSelection = DirectionalMappingsPreviewListBox.SelectedItem is DirectionalMappingPreviewItem;
        LoadMappingButton.IsEnabled = hasSelection;
        RemoveMappingButton.IsEnabled = hasSelection;
    }

    private void LoadSelectedDirectionalMapping_OnClick(object sender, RoutedEventArgs e)
    {
        if (DirectionalMappingsPreviewListBox.SelectedItem is not DirectionalMappingPreviewItem selected)
        {
            return;
        }

        DirectionalTokenComboBox.SelectedItem = _workingValues
            .FirstOrDefault(value => string.Equals(value, selected.Token, StringComparison.OrdinalIgnoreCase));
        TraversalDirectionComboBox.SelectedItem = selected.TraversalDirection.ToString();
    }

    private void RemoveSelectedDirectionalMapping_OnClick(object sender, RoutedEventArgs e)
    {
        if (DirectionalMappingsPreviewListBox.SelectedItem is not DirectionalMappingPreviewItem selected)
        {
            return;
        }

        _workingDirectionalMappings.Remove(selected.Token);
        RefreshDirectionalMappingsPreview();
    }

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        // If the user typed a token and clicks Save without pressing Add,
        // keep the pending token instead of silently dropping it.
        var result = TryAddTokenFromInput();
        if (result == AddTokenResult.Duplicate)
        {
            ShowTokenInputFeedback(result);
        }
        else
        {
            TokenInputFeedbackText.Visibility = Visibility.Collapsed;
            TokenInputFeedbackText.Text = string.Empty;
        }

        _targetValues.Clear();
        foreach (var value in _workingValues)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(trimmed)
                && !_targetValues.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
            {
                _targetValues.Add(trimmed);
            }
        }

        if (_isDirectionalMode && _targetDirectionalMappings is not null)
        {
            _targetDirectionalMappings.Clear();
            foreach (var token in _workingValues
                         .Select(static value => value.Trim())
                         .Where(static value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(static value => value, StringComparer.OrdinalIgnoreCase))
            {
                if (!_workingDirectionalMappings.TryGetValue(token, out var mappedDirection))
                {
                    continue;
                }

                _targetDirectionalMappings.Add(new DirectionalTraversalMapping
                {
                    Token = token,
                    TraversalDirection = mappedDirection
                });
            }
        }

        DialogResult = true;
    }

    private AddTokenResult TryAddTokenFromInput()
    {
        var candidate = TokenInputTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return AddTokenResult.Empty;
        }

        if (_workingValues.Any(existing => string.Equals(existing, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            TokenInputTextBox.Clear();
            TokenInputTextBox.Focus();
            return AddTokenResult.Duplicate;
        }

        _workingValues.Add(candidate);
        TokensListBox.SelectedItem = candidate;
        TokensListBox.ScrollIntoView(candidate);

        if (_isDirectionalMode && DirectionalTokenComboBox.SelectedItem is null)
        {
            DirectionalTokenComboBox.SelectedItem = candidate;
        }

        TokenInputTextBox.Clear();
        TokenInputTextBox.Focus();
        return AddTokenResult.Added;
    }

    private void ShowTokenInputFeedback(AddTokenResult result)
    {
        switch (result)
        {
            case AddTokenResult.Added:
                TokenInputFeedbackText.Visibility = Visibility.Collapsed;
                TokenInputFeedbackText.Text = string.Empty;
                break;
            case AddTokenResult.Duplicate:
                TokenInputFeedbackText.Text = "That token already exists in this scope list.";
                TokenInputFeedbackText.Visibility = Visibility.Visible;
                break;
            case AddTokenResult.Empty:
                TokenInputFeedbackText.Text = "Type a token before adding.";
                TokenInputFeedbackText.Visibility = Visibility.Visible;
                break;
        }
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private static IReadOnlyList<string> BuildTraversalDirectionChoices()
    {
        var choices = new List<string> { "(unmapped)" };
        choices.AddRange(Enum.GetNames<Direction10>());
        return choices;
    }

    private void RefreshDirectionalMappingsPreview()
    {
        if (!_isDirectionalMode)
        {
            return;
        }

        var previewItems = _workingDirectionalMappings
            .Where(pair => _workingValues.Any(value => string.Equals(value, pair.Key, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static pair => new DirectionalMappingPreviewItem
            {
                Token = pair.Key,
                TraversalDirection = pair.Value,
                DisplayText = $"{pair.Key} -> {pair.Value}"
            })
            .ToList();

        DirectionalMappingsPreviewListBox.ItemsSource = previewItems;
        DirectionalMappingsPreviewListBox.SelectedItem = null;
        LoadMappingButton.IsEnabled = false;
        RemoveMappingButton.IsEnabled = false;
    }
}
