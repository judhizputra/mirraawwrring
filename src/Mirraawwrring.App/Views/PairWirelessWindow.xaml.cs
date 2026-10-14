using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mirraawwrring.App.Interop;
using Mirraawwrring.App.ViewModels;

namespace Mirraawwrring.App.Views;

/// <summary>Dialog Pair wireless. Logika kotak 6 digit (auto-maju, backspace mundur, paste 6 digit sekaligus) ada di sini.</summary>
public partial class PairWirelessWindow : Window
{
    private readonly PairWirelessViewModel _viewModel;
    private readonly List<TextBox> _digits = [];
    private bool _syncing;

    public PairWirelessWindow(PairWirelessViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
        SourceInitialized += (_, _) => WindowTheming.Apply(this);
        viewModel.Completed += () => Dispatcher.InvokeAsync(() => DialogResult = true);

        foreach (var child in DigitPanel.Children)
        {
            if (child is TextBox box)
            {
                _digits.Add(box);
                box.PreviewTextInput += OnDigitPreviewTextInput;
                box.TextChanged += OnDigitTextChanged;
                box.PreviewKeyDown += OnDigitKeyDown;
                box.GotKeyboardFocus += (s, _) => (s as TextBox)?.SelectAll();
                DataObject.AddPastingHandler(box, OnDigitPasting);
            }
        }

        Loaded += (_, _) => AddressBox.Focus();
    }

    /// <summary>Serial yang tersambung setelah dialog ditutup dengan sukses.</summary>
    public string? ConnectedSerial => _viewModel.ConnectedSerial;

    private static void OnDigitPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !e.Text.All(char.IsAsciiDigit);
    }

    private void OnDigitPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text))
        {
            return;
        }

        var text = (string)e.DataObject.GetData(DataFormats.Text)!;
        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0)
        {
            e.CancelCommand();
            return;
        }

        e.CancelCommand();
        var start = digits.Length >= _digits.Count ? 0 : _digits.IndexOf((TextBox)sender);
        FillFrom(start, digits);
    }

    private void OnDigitTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncing)
        {
            return;
        }

        var box = (TextBox)sender;
        var index = _digits.IndexOf(box);
        var digits = new string(box.Text.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length > 1)
        {
            // Ketikan cepat / autofill lebih dari satu digit.
            FillFrom(index, digits);
            return;
        }

        if (digits != box.Text)
        {
            _syncing = true;
            box.Text = digits;
            _syncing = false;
        }

        if (digits.Length == 1 && index < _digits.Count - 1)
        {
            _digits[index + 1].Focus();
        }

        SyncCode();
    }

    private void OnDigitKeyDown(object sender, KeyEventArgs e)
    {
        var box = (TextBox)sender;
        var index = _digits.IndexOf(box);
        switch (e.Key)
        {
            case Key.Back when box.Text.Length == 0 && index > 0:
                _digits[index - 1].Text = string.Empty;
                _digits[index - 1].Focus();
                e.Handled = true;
                break;
            case Key.Left when index > 0:
                _digits[index - 1].Focus();
                e.Handled = true;
                break;
            case Key.Right when index < _digits.Count - 1:
                _digits[index + 1].Focus();
                e.Handled = true;
                break;
        }
    }

    private void FillFrom(int start, string digits)
    {
        _syncing = true;
        var d = 0;
        for (var i = start; i < _digits.Count && d < digits.Length; i++, d++)
        {
            _digits[i].Text = digits[d].ToString();
        }

        _syncing = false;
        SyncCode();
        var last = Math.Min(start + digits.Length, _digits.Count) - 1;
        _digits[Math.Max(last, 0)].Focus();
        if (last == _digits.Count - 1 && _viewModel.PrimaryCommand.CanExecute(null))
        {
            // Semua digit terisi dari paste: fokus ke tombol utama lewat Enter.
        }
    }

    private void SyncCode()
    {
        _viewModel.Code = string.Concat(_digits.Select(b => b.Text));
    }
}
