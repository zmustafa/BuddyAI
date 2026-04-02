namespace BuddyAI.Forms;

public sealed class TextInputDialog : Form
{
    private readonly TextBox _textBox = new();
    private readonly Button _btnOk = new();
    private readonly Button _btnCancel = new();

    public string InputText => _textBox.Text.Trim();

    public TextInputDialog(string title, string label, string initialValue = "")
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Size = new Size(520, 180);

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        layout.Controls.Add(new Label { Text = label, Dock = DockStyle.Top, AutoSize = true }, 0, 0);

        _textBox.Dock = DockStyle.Top;
        _textBox.Text = initialValue ?? string.Empty;
        layout.Controls.Add(_textBox, 0, 1);

        FlowLayoutPanel buttons = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Height = 36
        };
        layout.Controls.Add(buttons, 0, 2);

        _btnOk.Text = "OK";
        _btnOk.DialogResult = DialogResult.OK;
        _btnOk.Width = 90;
        _btnCancel.Text = "Cancel";
        _btnCancel.DialogResult = DialogResult.Cancel;
        _btnCancel.Width = 90;
        buttons.Controls.Add(_btnOk);
        buttons.Controls.Add(_btnCancel);

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;
    }
}
