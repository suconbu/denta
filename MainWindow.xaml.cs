﻿using System;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Reactive.Bindings;

namespace Suconbu.Dentacs
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        public enum ErrorState { None, ErrorWithoutMessage, ErrorWithMessage }

        public ReactiveProperty<string> RxResult { get; }
        public ReactiveProperty<string> RxErrorText { get; }
        public ReactiveProperty<string> RxUsageText { get; }
        public ReadOnlyReactivePropertySlim<ErrorState> RxErrorState { get; }
        public ReactiveProperty<double> RxZoomRatio { get; }
        public ReadOnlyReactivePropertySlim<double> RxMildZoomRatio { get; }
        public ReadOnlyReactivePropertySlim<double> RxMildMildZoomRatio { get; }
        public ReactiveProperty<int> RxZoomIndex { get; }
        public ReadOnlyReactivePropertySlim<string> RxTitleText { get; }
        public ReactiveProperty<bool> RxFullScreenEnabled { get; }
        public ReactiveProperty<bool> RxKeypadEnabled { get; }
        public ReactiveProperty<string> RxCurrentText { get; }
        public ReactiveProperty<int> RxSelectionLength { get; }
        public ReadOnlyReactivePropertySlim<bool> RxCaptionVisible { get; }
        public ReadOnlyReactivePropertySlim<bool> RxStatusVisible { get; }
        public ReadOnlyReactivePropertySlim<bool> RxKeypadVisible { get; }
        public ReadOnlyReactivePropertySlim<bool> RxFullScreenKeypadVisible { get; }
        public ReadOnlyReactivePropertySlim<bool> RxErrorTextVisible { get; }
        public ReadOnlyReactivePropertySlim<bool> RxCharInfoVisible { get; }
        public Color AccentColor = ((SolidColorBrush)SystemParameters.WindowGlassBrush).Color;

        readonly Calculator calculator = new Calculator();
        int lastLineIndex = -1;
        int zoomIndexBackup = 0;
        int fullScreenZoomIndexBackup = 4;
        double maxWidthBackup = SystemParameters.WorkArea.Width;

        static readonly double[] kZoomTable = new[] { 1.0, 1.5, 2.0, 3.0, 4.0 };
        static readonly int kCopyFlashInterval = 100;

        public MainWindow()
        {
            InitializeComponent();

            this.DataContext = this;

            this.RxResult = new ReactiveProperty<string>();
            this.RxErrorText = new ReactiveProperty<string>();
            this.RxErrorState = this.RxErrorText.Select(x =>
                x == null ? ErrorState.None :
                x == string.Empty ? ErrorState.ErrorWithoutMessage :
                ErrorState.ErrorWithMessage)
                .ToReadOnlyReactivePropertySlim();
            this.RxUsageText = new ReactiveProperty<string>();
            this.RxZoomIndex = new ReactiveProperty<int>();
            this.RxZoomRatio = this.RxZoomIndex.Select(x => kZoomTable[x]).ToReactiveProperty();
            this.RxMildZoomRatio = this.RxZoomRatio.Select(x => ((3 - 1) + x) / 3).ToReadOnlyReactivePropertySlim();
            this.RxMildMildZoomRatio = this.RxZoomRatio.Select(x => ((5 - 1) + x) / 5).ToReadOnlyReactivePropertySlim();
            this.RxTitleText = this.RxZoomRatio.Select(_ => this.MakeTitleText()).ToReadOnlyReactivePropertySlim();
            this.RxFullScreenEnabled = new ReactiveProperty<bool>(false);
            this.RxFullScreenEnabled.Subscribe(x => this.IsFullScreenChanged(x));
            this.RxKeypadEnabled = new ReactiveProperty<bool>();
            this.RxKeypadEnabled.Subscribe(x => this.InputTextBox.Focus());
            this.RxCurrentText = new ReactiveProperty<string>();
            this.RxSelectionLength = new ReactiveProperty<int>();

            this.RxCaptionVisible = this.RxFullScreenEnabled.Select(x => !x).ToReadOnlyReactivePropertySlim();
            this.RxStatusVisible = this.RxFullScreenEnabled.Select(x => !x).ToReadOnlyReactivePropertySlim();
            this.RxCharInfoVisible = this.RxUsageText.Select(x => string.IsNullOrEmpty(x)).ToReadOnlyReactivePropertySlim();
            this.RxKeypadVisible = Observable.CombineLatest(
                this.RxFullScreenEnabled, this.RxKeypadEnabled, (f, k) => !f && k)
                .ToReadOnlyReactivePropertySlim();
            this.RxFullScreenKeypadVisible = Observable.CombineLatest(
                this.RxFullScreenEnabled, this.RxKeypadEnabled, (f, k) => f && k)
                .ToReadOnlyReactivePropertySlim();
            this.RxErrorTextVisible = Observable.CombineLatest(
                this.RxErrorText, this.RxUsageText, (e, u) => !string.IsNullOrEmpty(e) && string.IsNullOrEmpty(u))
                .ToReadOnlyReactivePropertySlim();

            this.KeypadPanel.ItemMouseEnter += (s, item) => { this.RxUsageText.Value = item.Usage; };
            this.KeypadPanel.ItemMouseLeave += (s, item) => { this.RxUsageText.Value = null; };
            this.KeypadPanel.ItemClick += (s, item) => this.KeypadPanel_ItemClick(item);

            this.FullScreenKeypadPanel.ItemMouseEnter += (s, item) => { this.RxUsageText.Value = item.Usage; };
            this.FullScreenKeypadPanel.ItemMouseLeave += (s, item) => { this.RxUsageText.Value = null; };
            this.FullScreenKeypadPanel.ItemClick += (s, item) => this.KeypadPanel_ItemClick(item);
        }

        string MakeTitleText()
        {
            return "dentacs" +
                (this.RxZoomRatio.Value != 1.0 ? $" {this.RxZoomRatio.Value * 100:#}%" : "");
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            this.InputTextBox.Focus();
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);

            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                this.ChangeZoom(0 < e.Delta ? +1 : -1);
            }
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            var handled = true;
            var control = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

            if (control && e.Key == Key.PageUp)
            {
                this.ChangeZoom(+1);
            }
            else if (control && e.Key == Key.PageDown)
            {
                this.ChangeZoom(-1);
            }
            else if (e.Key == Key.F11)
            {
                // Toggle full-screen
                this.RxFullScreenEnabled.Value = !this.RxFullScreenEnabled.Value;
            }
            else if (e.Key == Key.Tab)
            {
                // Toggle keypad
                this.RxKeypadEnabled.Value = !this.RxKeypadEnabled.Value;
            }
            else
            {
                handled = false;
            }

            e.Handled = handled;
        }

        void ChangeZoom(int offset)
        {
            this.RxZoomIndex.Value = Math.Clamp(this.RxZoomIndex.Value + offset, 0, kZoomTable.Length - 1);
        }

        void KeypadPanel_ItemClick(KeypadPanel.Item item)
        {
            var target = this.InputTextBox;
            var data = item.Data;
            target.Focus();
            if (item.Command == KeypadPanel.Command.BackSpace)
            {
                System.Windows.Forms.SendKeys.SendWait("{BACKSPACE}");
            }
            else if (item.Command == KeypadPanel.Command.Undo)
            {
                target.Undo();
            }
            else if (item.Command == KeypadPanel.Command.Redo)
            {
                target.Redo();
            }
            else if (item.Command == KeypadPanel.Command.Clear)
            {
                target.Clear();
            }
            else
            {
                if (data.EndsWith("()"))
                {
                    var name = data.Substring(0, data.Length - 2);
                    var prevSelectedLength = target.SelectedText.Length;
                    var selectionEnd = target.SelectionStart + target.SelectionLength;
                    if (0 < name.Length &&
                        selectionEnd < target.Text.Length &&
                        target.Text[selectionEnd] == '(')
                    {
                        target.SelectedText = name;
                        target.SelectionLength = 0;
                    }
                    else
                    {
                        target.SelectedText = $"{name}({target.SelectedText})";
                        target.SelectionLength = prevSelectedLength;
                    }
                    target.SelectionStart += name.Length + 1;
                }
                else
                {
                    target.SelectedText = data;
                    target.SelectionLength = 0;
                    target.SelectionStart += data.Length;
                }
            }
        }

        void IsFullScreenChanged(bool enable)
        {
            if (enable)
            {
                this.zoomIndexBackup = this.RxZoomIndex.Value;
                this.RxZoomIndex.Value = this.fullScreenZoomIndexBackup;
                this.maxWidthBackup = this.MaxWidth;
                this.MaxWidth = Double.PositiveInfinity;
                this.WindowBorder.BorderThickness = new Thickness(0);
                this.SizeToContent = SizeToContent.Manual;
                //this.WindowStyle = WindowStyle.None;
                this.WindowState = WindowState.Maximized;
                this.Topmost = false;
            }
            else
            {
                if (this.IsLoaded)
                {
                    this.fullScreenZoomIndexBackup = this.RxZoomIndex.Value;
                }
                //this.WindowStyle = WindowStyle.SingleBorderWindow;
                this.WindowState = WindowState.Normal;
                this.SizeToContent = SizeToContent.WidthAndHeight;
                this.Topmost = true;
                this.MaxWidth = this.maxWidthBackup;
                this.WindowBorder.BorderThickness = new Thickness(1);
                this.RxZoomIndex.Value = this.zoomIndexBackup;
            }
        }

        private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            this.lastLineIndex = -1;
        }

        private void InputTextBox_SelectionChanged(object sender, RoutedEventArgs e)
        {
            if (!this.IsInitialized) return;

            var caretIndex = this.InputTextBox.CaretIndex;
            var lineIndex = this.InputTextBox.GetLineIndexFromCharacterIndex(caretIndex);
            var lines = this.InputTextBox.Text.Split(Environment.NewLine);
            // GetLineText does not work in fullscreen mode.
            var currentLine = lines[lineIndex]; //this.InputTextBox.GetLineText(lineIndex);
            var selectedText = this.InputTextBox.SelectedText;

            if (lineIndex != this.lastLineIndex || this.RxSelectionLength.Value != selectedText.Length)
            {
                this.calculator.Reset();
                for (int i = 0; i <= lineIndex; i++)
                {
                    this.calculator.Calculate(lines[i]);
                }
                if (0 < selectedText.Length)
                {
                    this.calculator.Calculate(selectedText);
                }
                if (this.calculator.Error == null)
                {
                    this.RxResult.Value = this.calculator.Result.ToString();
                }
                this.RxErrorText.Value = this.calculator.Error;
                this.lastLineIndex = lineIndex;
            }

            if (selectedText.Length == 0)
            {
                int lineStartIndex = lines.Take(lineIndex).Select(line => line.Length + Environment.NewLine.Length).Sum();
                int charIndexOfLine = caretIndex - lineStartIndex;
                var offset = (charIndexOfLine < currentLine.Length) ? 0 : -1;
                selectedText = CharInfoConvertHelper.GetUnicodeElement(currentLine, charIndexOfLine + offset);
            }
            this.RxCurrentText.Value = selectedText;
            this.RxSelectionLength.Value = this.InputTextBox.SelectionLength;
        }

        public static RoutedCommand CopyCommand = new RoutedCommand();

        void CopyCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Blink the selection area to show the value has been copied
            var textBox = e.Parameter as TextBox;
            var copyText = this.GetResultTextForCopy(textBox);
            if (copyText == null) return;
            Clipboard.SetText(copyText);
            var visibilityBackup = textBox.Visibility;
            textBox.Visibility = Visibility.Hidden;
            Task.Delay(kCopyFlashInterval).ContinueWith(x => { this.Dispatcher.Invoke(() => { textBox.Visibility = visibilityBackup; }); });
        }

        void CopyCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = (this.GetResultTextForCopy(e.Parameter as TextBox) != null);
        }

        string GetResultTextForCopy(TextBox textBox)
        {
            if (textBox == null) return null;
            if (!int.TryParse((string)textBox.Tag, out var radix)) return null;
            if (!decimal.TryParse(this.RxResult.Value, out var number)) return null;
            return ResultConvertHelper.ConvertToResultString(number, radix, ResultConvertHelper.Styles.Prefix);
        }

        private void CharInfo_Click(object sender, RoutedEventArgs e)
        {
            var text = CharInfoConvertHelper.ConvertToElementInfoString(this.RxCurrentText.Value, false);
            Clipboard.SetText(text);
            var item = sender as Control;
            item.Visibility = Visibility.Hidden;
            Task.Delay(kCopyFlashInterval).ContinueWith(x => { this.Dispatcher.Invoke(() => { item.Visibility = Visibility.Visible; }); });
        }

        private void FullScreenCloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.RxFullScreenEnabled.Value = false;
        }

        private void Caption_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.RxFullScreenEnabled.Value = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
