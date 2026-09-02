using EonaCat.FileSplitter.Models;
using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace EonaCat.FileSplitter;

public sealed class MainForm : Form
{
    // ============================================================
    // CONTROLS
    // ============================================================

    private readonly TabControl tabs = new();

    private readonly TextBox splitSource;
    private readonly TextBox splitPackage;

    private readonly List<string> splitSourceFiles = new();

    private readonly ListBox splitSourceList = new() { SelectionMode = SelectionMode.None };

    private readonly TextBox assemblePackage;
    private readonly TextBox assembleDestination;

    private readonly ListBox assembleFilesList = new() { SelectionMode = SelectionMode.One };

    private List<PackageFileInfo> assemblePackageFiles = new();

    private readonly Button splitButton;
    private readonly Button assembleButton;
    private readonly Button assembleAllButton;

    private readonly NumericUpDown chunkSize = new()
    {
        Minimum = 1,
        Maximum = 1024 * 1024,
        Value = 64,
        DecimalPlaces = 0,
        ThousandsSeparator = true
    };

    private readonly ComboBox chunkUnit = new() { DropDownStyle = ComboBoxStyle.DropDownList };

    private readonly CheckBox compress =
        new() { Text = "Compress chunks with GZip", AutoSize = true };

    private readonly ProgressBar progress =
        new() { Minimum = 0, Maximum = 1000, Style = ProgressBarStyle.Continuous };

    private readonly Label status = new() { AutoSize = false };

    private readonly Label details = new() { AutoSize = false };

    private readonly Label splitInfo = new() { AutoSize = false };

    private readonly Label assembleInfo = new() { AutoSize = false };

    // ============================================================
    // COLORS
    // ============================================================

    private static readonly Color Background = Color.FromArgb(13, 14, 20);

    private static readonly Color Surface = Color.FromArgb(22, 24, 32);

    private static readonly Color Surface2 = Color.FromArgb(32, 36, 46);

    private static readonly Color SurfaceHover = Color.FromArgb(40, 45, 58);

    private static readonly Color Border = Color.FromArgb(50, 55, 70);

    private static readonly Color BorderLight = Color.FromArgb(70, 77, 95);

    private static readonly Color Foreground = Color.FromArgb(240, 242, 246);

    private static readonly Color ForegroundSecondary = Color.FromArgb(210, 215, 225);

    private static readonly Color Muted = Color.FromArgb(130, 140, 160);

    private static readonly Color MutedLight = Color.FromArgb(170, 180, 195);

    private static readonly Color Accent = Color.FromArgb(82, 132, 245);

    private static readonly Color AccentHover = Color.FromArgb(102, 152, 255);

    private static readonly Color AccentActive = Color.FromArgb(62, 112, 225);

    private static readonly Color Success = Color.FromArgb(91, 182, 95);

    private static readonly Color Warning = Color.FromArgb(255, 184, 77);

    private static readonly Color Error = Color.FromArgb(229, 76, 76);

    // ============================================================
    // DPI / SCALING
    // ============================================================

    private const float DesignDpi = 96f;

    private float DpiScale => DeviceDpi <= 0 ? 1f : DeviceDpi / DesignDpi;

    private int Dpi(int value) => Math.Max(1, (int)Math.Round(value * DpiScale));

    private Padding DpiPadding(int left, int top, int right,
                               int bottom) => new(Dpi(left), Dpi(top), Dpi(right), Dpi(bottom));

    private Size DpiSize(int width, int height) => new(Dpi(width), Dpi(height));

    // ============================================================
    // CONSTRUCTOR
    // ============================================================

    public MainForm()
    {
        splitSource = CreateTextBox();
        splitPackage = CreateTextBox();

        assemblePackage = CreateTextBox();
        assembleDestination = CreateTextBox();

        splitButton = CreatePrimaryButton("Split Files");
        assembleButton = CreatePrimaryButton("Assemble Selected");
        assembleAllButton = CreatePrimaryButton("Assemble All Files");

        InitializeForm();
        BuildLayout();
        PopulateChunkUnits();
        WireEvents();

        UpdateChunkInfo(null, EventArgs.Empty);
    }

    // ============================================================
    // FORM INITIALIZATION
    // ============================================================

    private void InitializeForm()
    {
        Text = "EonaCat File Transport";

        AutoScaleMode = AutoScaleMode.Dpi;

        AutoScaleDimensions = new SizeF(DesignDpi, DesignDpi);

        StartPosition = FormStartPosition.CenterScreen;

        BackColor = Background;
        ForeColor = Foreground;

        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        DoubleBuffered = true;

        FormBorderStyle = FormBorderStyle.Sizable;

        ClientSize = DpiSize(1080, 768);
        MinimumSize = DpiSize(900, 650);

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer,
                 true);
    }

    private void WireEvents()
    {
        chunkSize.ValueChanged += UpdateChunkInfo;
        chunkUnit.SelectedIndexChanged += UpdateChunkInfo;
        compress.CheckedChanged += UpdateChunkInfo;

        assemblePackage.Leave += async (_, _) => await LoadPackageFilesAsync();

        DpiChanged += (_, _) => {
            PerformLayout();
            Invalidate(true);
        };

        splitButton.Click += async (_, _) => await SplitAsync();

        assembleButton.Click += async (_, _) => await AssembleAsync();

        assembleAllButton.Click += async (_, _) => await AssembleAllAsync();

        assembleFilesList.SelectedIndexChanged += OnAssembleSelectionChanged;
    }

    // ============================================================
    // INITIALIZATION
    // ============================================================

    private void PopulateChunkUnits()
    {
        chunkUnit.Items.AddRange(new object[] { "KB", "MB", "GB" });

        chunkUnit.SelectedItem = "MB";
    }

    // ============================================================
    // MAIN LAYOUT
    // ============================================================

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Background,
            Padding = DpiPadding(16, 14, 16, 12),
            ColumnCount = 1,
            RowCount = 3,
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            Margin = new Padding(0),
            AutoScroll = false
        };

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(CreateHeader(), 0, 0);

        root.Controls.Add(CreateTabs(), 0, 1);

        root.Controls.Add(CreateStatusBar(), 0, 2);

        Controls.Add(root);
    }

    // ============================================================
    // HEADER
    // ============================================================

    private Control CreateHeader()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(Dpi(2), 0, Dpi(2), Dpi(8)),
            Padding = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            AutoSize = true
        };

        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title =
            new Label
            {
                Text = "EonaCat File Transport",
                Font = new Font("Segoe UI Semibold", 24F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Foreground,
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 0, Dpi(2))
            };

        var subtitle =
            new Label
            {
                Text = "Fast, reliable file splitting and assembly",
                Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
                ForeColor = MutedLight,
                AutoSize = true,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };

        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(subtitle, 0, 1);

        return panel;
    }

    // ============================================================
    // TABS
    // ============================================================

    private Control CreateTabs()
    {
        tabs.Dock = DockStyle.Fill;
        tabs.Appearance = TabAppearance.Buttons;
        tabs.SizeMode = TabSizeMode.Fixed;

        tabs.ItemSize = DpiSize(190, 40);

        tabs.DrawMode = TabDrawMode.OwnerDrawFixed;

        tabs.Padding = new Point(Dpi(12), Dpi(3));

        tabs.Margin = new Padding(0, Dpi(4), 0, Dpi(8));

        tabs.DrawItem += DrawTab;

        var splitTab =
            new TabPage
            {
                Text = "SPLIT FILES",
                BackColor = Background,
                Padding = DpiPadding(10, 10, 10, 10),
                UseVisualStyleBackColor = false
            };

        var assembleTab =
            new TabPage
            {
                Text = "ASSEMBLE FILES",
                BackColor = Background,
                Padding = DpiPadding(10, 10, 10, 10),
                UseVisualStyleBackColor = false
            };

        splitTab.Controls.Add(CreateSplitPage());
        assembleTab.Controls.Add(CreateAssemblePage());

        tabs.TabPages.Add(splitTab);
        tabs.TabPages.Add(assembleTab);

        return tabs;
    }

    private void DrawTab(object? sender, DrawItemEventArgs e)
    {
        bool selected = e.Index == tabs.SelectedIndex;

        var bounds = e.Bounds;

        using var backgroundBrush = new SolidBrush(selected ? Surface2 : Background);

        e.Graphics.FillRectangle(backgroundBrush, bounds);

        using var textBrush = new SolidBrush(selected ? Foreground : MutedLight);

        using var font = new Font("Segoe UI Semibold", 10F, FontStyle.Bold, GraphicsUnit.Point);

        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        e.Graphics.DrawString(tabs.TabPages[e.Index].Text, font, textBrush, bounds, format);

        if (!selected)
        {
            return;
        }

        using var accentBrush = new SolidBrush(Accent);

        int inset = Dpi(15);
        int thickness = Dpi(3);

        e.Graphics.FillRectangle(accentBrush, bounds.X + inset, bounds.Bottom - thickness,
                                 Math.Max(1, bounds.Width - inset * 2), thickness);
    }

    // ============================================================
    // SPLIT PAGE
    // ============================================================

    private Control CreateSplitPage()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0),
            Margin = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            AutoScroll = false
        };

        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var card = CreateCard();

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = DpiPadding(22, 18, 22, 18),
            Margin = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };

        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        content.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi(10)));

        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        content.Controls.Add(CreatePathField("SOURCE FILE(S)",
                                             "Select one or more files you want to split", splitSource,
                                             "Browse", BrowseSplitSource),
                             0, 0);

        content.Controls.Add(CreateFilesListPanel("SELECTED FILES", splitSourceList), 0, 1);

        content.Controls.Add(CreatePathField("PACKAGE FOLDER",
                                             "Folder where the chunks will be created", splitPackage,
                                             "Browse", BrowseSplitPackage),
                             0, 3);

        content.Controls.Add(CreateSplitSettingsPanel(), 0, 4);

        card.Controls.Add(content);

        page.Controls.Add(card, 0, 0);

        page.Controls.Add(CreateInfoCard(splitInfo), 0, 1);

        page.Controls.Add(CreateActionPanel(splitButton, "Start splitting the selected files"), 0, 2);

        return page;
    }

    // ============================================================
    // SPLIT SETTINGS
    // ============================================================

    private Control CreateSplitSettingsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, Dpi(10), 0, 0),
            Padding = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));

        panel.Controls.Add(CreateChunkSettings(), 0, 0);

        var compressionPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = DpiPadding(12, 7, 4, 0),
            Margin = new Padding(0)
        };

        compress.ForeColor = Foreground;
        compress.BackColor = Surface;
        compress.AutoSize = true;
        compress.Anchor = AnchorStyles.Left;

        compressionPanel.Controls.Add(compress);

        panel.Controls.Add(compressionPanel, 1, 0);

        return panel;
    }

    // ============================================================
    // ASSEMBLE PAGE
    // ============================================================

    private Control CreateAssemblePage()
    {
        var page = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(0),
            Margin = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            AutoScroll = false
        };

        page.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        page.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var card = CreateCard();

        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = DpiPadding(22, 18, 22, 18),
            Margin = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };

        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        content.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi(10)));

        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        content.Controls.Add(CreatePathField("PACKAGE FOLDER", "Folder containing the transport chunks",
                                             assemblePackage, "Browse", BrowseAssemblePackage),
                             0, 0);

        content.Controls.Add(CreateFilesListPanel("FILES IN THIS PACKAGE", assembleFilesList), 0, 1);

        content.Controls.Add(
            CreatePathField("DESTINATION FILE",
                            "Output filename. The original filename is selected automatically.",
                            assembleDestination, "Browse", BrowseAssembleDestination),
            0, 3);

        card.Controls.Add(content);

        page.Controls.Add(card, 0, 0);

        page.Controls.Add(CreateInfoCard(assembleInfo, "Select a package folder to see its files"), 0,
                          1);

        page.Controls.Add(CreateActionPanel(assembleButton, "Rebuild the selected file"), 0, 2);

        page.Controls.Add(CreateActionPanel(assembleAllButton, "Rebuild every file in the package"), 0,
                          3);

        page.Controls.Add(new Panel { Height = Dpi(4), BackColor = Background }, 0, 4);

        return page;
    }

    // ============================================================
    // PATH FIELD
    // ============================================================

    private Control CreatePathField(string title, string description, TextBox textbox,
                                    string buttonText, EventHandler handler)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            AutoSize = true
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dpi(110)));

        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label =
            new Label
            {
                Text = title,
                ForeColor = Foreground,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, Dpi(2))
            };

        var hint = new Label
        {
            Text = description,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point),
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, Dpi(4))
        };

        var titlePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            AutoSize = true
        };

        titlePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        titlePanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        titlePanel.Controls.Add(label, 0, 0);
        titlePanel.Controls.Add(hint, 0, 1);

        panel.Controls.Add(titlePanel, 0, 0);

        panel.SetColumnSpan(titlePanel, 2);

        textbox.Dock = DockStyle.Fill;

        textbox.MinimumSize = new Size(0, Dpi(34));

        textbox.Margin = new Padding(0, Dpi(2), Dpi(10), Dpi(2));

        var browse = CreateSecondaryButton(buttonText);

        browse.Dock = DockStyle.Fill;

        browse.MinimumSize = DpiSize(90, 34);

        browse.Margin = new Padding(0, Dpi(2), 0, Dpi(2));

        browse.Click += handler;

        panel.Controls.Add(textbox, 0, 1);

        panel.Controls.Add(browse, 1, 1);

        return panel;
    }

    // ============================================================
    // CHUNK SETTINGS
    // ============================================================

    private Control CreateChunkSettings()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            AutoSize = true
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label =
            new Label
            {
                Text = "CHUNK SIZE",
                ForeColor = Foreground,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, Dpi(14), Dpi(5))
            };

        var controls = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, Dpi(1), 0, 0),
            Margin = new Padding(0)
        };

        StyleNumeric(chunkSize);

        chunkSize.MinimumSize = DpiSize(120, 34);

        chunkSize.Width = Dpi(140);
        chunkSize.Height = Dpi(34);

        chunkUnit.MinimumSize = DpiSize(78, 34);

        chunkUnit.Width = Dpi(88);
        chunkUnit.Height = Dpi(34);

        chunkUnit.BackColor = Surface2;
        chunkUnit.ForeColor = Foreground;
        chunkUnit.FlatStyle = FlatStyle.Flat;

        chunkSize.Margin = new Padding(0, 0, Dpi(8), 0);

        chunkUnit.Margin = new Padding(0);

        controls.Controls.Add(chunkSize);
        controls.Controls.Add(chunkUnit);

        panel.Controls.Add(label, 0, 0);

        panel.Controls.Add(controls, 0, 1);

        panel.SetColumnSpan(controls, 2);

        return panel;
    }

    // ============================================================
    // FILE LIST
    // ============================================================

    private Control CreateFilesListPanel(string title, ListBox listBox)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };

        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var label =
            new Label
            {
                Text = title,
                ForeColor = Foreground,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point),
                AutoSize = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, Dpi(4))
            };

        listBox.Dock = DockStyle.Fill;

        listBox.MinimumSize = new Size(0, Dpi(80));

        listBox.BackColor = Surface2;
        listBox.ForeColor = Foreground;
        listBox.BorderStyle = BorderStyle.FixedSingle;

        listBox.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        listBox.IntegralHeight = false;
        listBox.Margin = new Padding(0);
        listBox.HorizontalScrollbar = true;
        listBox.HorizontalExtent = Dpi(500);

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(listBox, 0, 1);

        return panel;
    }

    // ============================================================
    // INFO CARD
    // ============================================================

    private Control CreateInfoCard(Label label, string text = "")
    {
        if (!string.IsNullOrEmpty(text))
        {
            label.Text = text;
        }

        label.ForeColor = Muted;

        label.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        label.AutoSize = false;
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;

        label.Padding = DpiPadding(14, 0, 14, 0);

        label.Margin = new Padding(0);

        var panel =
            new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Surface,
                MinimumSize = DpiSize(0, 50),
                Margin = new Padding(0, Dpi(10), 0, Dpi(8))
            };

        panel.Paint += (_, e) => {
            using var pen = new Pen(Border, Math.Max(1, DpiScale));

            DrawRoundedBorder(e.Graphics, pen, 0, 0, panel.Width - 1, panel.Height - 1, Dpi(7));
        };

        panel.Controls.Add(label);

        return panel;
    }

    // ============================================================
    // ACTION PANEL
    // ============================================================

    private Control CreateActionPanel(Button button, string description)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, Dpi(4), 0, Dpi(4)),
            Margin = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Dpi(210)));

        var label = new Label
        {
            Text = description,
            ForeColor = Muted,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point),
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };

        button.Dock = DockStyle.Fill;

        button.MinimumSize = DpiSize(180, 42);

        button.Margin = new Padding(Dpi(12), 0, 0, 0);

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(button, 1, 0);

        return panel;
    }

    // ============================================================
    // STATUS BAR
    // ============================================================

    private Control CreateStatusBar()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = DpiPadding(4, 4, 4, 0),
            Margin = new Padding(0),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.None,
            AutoSize = true
        };

        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, Dpi(14)));

        status.Text = "Ready";
        status.ForeColor = Foreground;

        status.Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold, GraphicsUnit.Point);

        status.Dock = DockStyle.Fill;
        status.Height = Dpi(20);
        status.TextAlign = ContentAlignment.MiddleLeft;

        details.Text = "";
        details.ForeColor = Muted;

        details.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

        details.Dock = DockStyle.Fill;
        details.Height = Dpi(18);
        details.TextAlign = ContentAlignment.MiddleLeft;

        progress.Dock = DockStyle.Fill;
        progress.BackColor = Surface;
        progress.ForeColor = Accent;
        progress.Margin = new Padding(0);

        panel.Controls.Add(status, 0, 0);
        panel.Controls.Add(details, 0, 1);
        panel.Controls.Add(progress, 0, 2);

        return panel;
    }

    // ============================================================
    // CARD
    // ============================================================

    private Panel CreateCard()
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Surface,
            Padding = new Padding(Dpi(1)),
            Margin = new Padding(0, 0, 0, Dpi(4))
        };

        card.Paint += (_, e) => {
            DrawShadow(e.Graphics, 0, 0, card.Width, card.Height);

            using var pen = new Pen(Border, Math.Max(1, DpiScale));

            DrawRoundedBorder(e.Graphics, pen, 0, 0, card.Width - 1, card.Height - 1, Dpi(8));
        };

        return card;
    }

    // ============================================================
    // DRAWING HELPERS
    // ============================================================

    private static void DrawRoundedRectangle(Graphics g, Brush brush, int x, int y, int width,
                                             int height, int radius)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        radius = Math.Min(radius, Math.Min(width / 2, height / 2));

        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var path = CreateRoundedPath(x, y, width, height, radius);

        g.FillPath(brush, path);
    }

    private static void DrawRoundedBorder(Graphics g, Pen pen, int x, int y, int width, int height,
                                          int radius)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        radius = Math.Min(radius, Math.Min(width / 2, height / 2));

        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var path = CreateRoundedPath(x, y, width, height, radius);

        g.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedPath(int x, int y, int width, int height, int radius)
    {
        var path = new GraphicsPath();

        int diameter = radius * 2;

        path.AddArc(x, y, diameter, diameter, 180, 90);

        path.AddArc(x + width - diameter, y, diameter, diameter, 270, 90);

        path.AddArc(x + width - diameter, y + height - diameter, diameter, diameter, 0, 90);

        path.AddArc(x, y + height - diameter, diameter, diameter, 90, 90);

        path.CloseFigure();

        return path;
    }

    private void DrawShadow(Graphics g, int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            return;
        }

        int shadowHeight = Dpi(5);

        using var shadowBrush =
            new LinearGradientBrush(new Point(x, y + height), new Point(x, y + height + shadowHeight),
                                    Color.FromArgb(0, 0, 0, 0), Color.FromArgb(35, 0, 0, 0));

        g.FillRectangle(shadowBrush, x, y + height, width, shadowHeight);
    }

    // ============================================================
    // BROWSERS
    // ============================================================

    private void BrowseSplitSource(object? sender, EventArgs e)
    {
        using var dialog =
            new OpenFileDialog
            {
                Title = "Select source file(s)",
                Filter = "All files (*.*)|*.*",
                Multiselect = true,
                CheckFileExists = true,
                RestoreDirectory = true
            };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        splitSourceFiles.Clear();

        splitSourceFiles.AddRange(dialog.FileNames);

        RefreshSplitSourceDisplay();
    }

    private void RefreshSplitSourceDisplay()
    {
        splitSource.Text = splitSourceFiles.Count switch
        {
            0 => "",
            1 => splitSourceFiles[0],
            _ => $"{splitSourceFiles.Count:N0} files selected"
        };

        splitSourceList.Items.Clear();

        foreach (string file in splitSourceFiles)
        {
            splitSourceList.Items.Add(Path.GetFileName(file));
        }
    }

    private void BrowseSplitPackage(object? sender, EventArgs e)
    {
        using var dialog =
            new FolderBrowserDialog
            {
                Description = "Select where the package should be created",

                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        splitPackage.Text = dialog.SelectedPath;
    }

    private async void BrowseAssemblePackage(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select the transport package",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        assemblePackage.Text = dialog.SelectedPath;

        await LoadPackageFilesAsync();
    }

    // ============================================================
    // ASSEMBLE DESTINATION
    // ============================================================

    private void BrowseAssembleDestination(object? sender, EventArgs e)
    {
        string defaultName = "assembled.bin";

        int selectedIndex = assembleFilesList.SelectedIndex;

        if (selectedIndex >= 0 && selectedIndex < assemblePackageFiles.Count)
        {
            defaultName = assemblePackageFiles[selectedIndex].OriginalFileName;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Choose destination file",
            FileName = defaultName,
            Filter = "All files (*.*)|*.*",
            OverwritePrompt = true,
            RestoreDirectory = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        assembleDestination.Text = dialog.FileName;
    }

    // ============================================================
    // PACKAGE FILE LIST
    // ============================================================

    private async Task LoadPackageFilesAsync()
    {
        assembleFilesList.Items.Clear();
        assemblePackageFiles = new List<PackageFileInfo>();
        assembleDestination.Clear();

        if (string.IsNullOrWhiteSpace(assemblePackage.Text) ||
            !Directory.Exists(assemblePackage.Text))
        {
            assembleInfo.Text = "Select a valid package folder.";

            return;
        }

        try
        {
            assemblePackageFiles = await new FileAssembler().ListPackageFilesAsync(assemblePackage.Text);

            foreach (PackageFileInfo file in assemblePackageFiles)
            {
                assembleFilesList.Items.Add($"{file.OriginalFileName}   " +
                                            $"({FormatBytes(file.FileLength)})");
            }

            if (assembleFilesList.Items.Count > 0)
            {
                assembleFilesList.SelectedIndex = 0;
            }

            assembleInfo.Text = assemblePackageFiles.Count switch
            {
                0 => "No files were found in this package.",

                1 => $"1 file ready to assemble: " + $"{assemblePackageFiles[0].OriginalFileName}",

                _ => $"{assemblePackageFiles.Count:N0} " + "files ready to assemble."
            };
        }
        catch (Exception ex)
        {
            assembleInfo.Text = $"Could not read package: {ex.Message}";
        }
    }

    private void OnAssembleSelectionChanged(object? sender, EventArgs e)
    {
        int selectedIndex = assembleFilesList.SelectedIndex;

        if (selectedIndex < 0 || selectedIndex >= assemblePackageFiles.Count)
        {
            return;
        }

        var selected = assemblePackageFiles[selectedIndex];

        string? existingDirectory = string.IsNullOrWhiteSpace(assembleDestination.Text)
                                        ? null
                                        : Path.GetDirectoryName(assembleDestination.Text);

        assembleDestination.Text = string.IsNullOrEmpty(existingDirectory)
                                       ? selected.OriginalFileName
                                       : Path.Combine(existingDirectory, selected.OriginalFileName);
    }

    // ============================================================
    // OPTIONS
    // ============================================================

    private FileTransportOptions CreateOptions() => new()
    {
        ChunkSizeBytes = checked((int)(chunkSize.Value * UnitMultiplier())),

        BufferSizeBytes = 1024 * 1024,

        CompressChunks = compress.Checked
    };

    private long UnitMultiplier()
    {
        return chunkUnit.SelectedItem?.ToString() switch
        {
            "KB" => 1024L,

            "GB" => 1024L * 1024L * 1024L,

            _ => 1024L * 1024L
        };
    }

    // ============================================================
    // CHUNK INFO
    // ============================================================

    private void UpdateChunkInfo(object? sender, EventArgs e)
    {
        if (chunkUnit.SelectedItem == null)
        {
            return;
        }

        long bytes = (long)(chunkSize.Value * UnitMultiplier());

        splitInfo.Text = $"Each chunk: {FormatBytes(bytes)}   •   " +
                         (compress.Checked ? "GZip compression enabled" : "Compression disabled");
    }

    // ============================================================
    // SPLIT
    // ============================================================

    private async Task SplitAsync()
    {
        if (splitSourceFiles.Count == 0 || string.IsNullOrWhiteSpace(splitPackage.Text))
        {
            ShowInformation("Select at least one source file and a package folder.",
                            "Missing information");

            return;
        }

        if (!Directory.Exists(splitPackage.Text))
        {
            try
            {
                Directory.CreateDirectory(splitPackage.Text);
            }
            catch (Exception ex)
            {
                ShowError($"Could not create package folder:\n\n{ex.Message}", "Folder error");

                return;
            }
        }

        Toggle(false);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            FileTransportOptions options = CreateOptions();

            var progressReporter = new Progress<MultiFileTransferProgress>(UpdateMultiProgress);

            var manifests = await new FileSplitter().SplitManyAsync(splitSourceFiles, splitPackage.Text,
                                                                    options, progressReporter);

            long totalBytes = manifests.Sum(m => m.FileLength);

            int totalChunks = manifests.Sum(m => m.Chunks.Count);

            status.Text = "Split complete";

            details.Text = manifests.Count == 1
                               ? $"{totalChunks:N0} chunks  •  " + $"{FormatBytes(totalBytes)}  •  " +
                                     $"SHA-256 {manifests[0].OverallSha256}  •  " +
                                     $"{stopwatch.Elapsed:hh\\:mm\\:ss}"
                               : $"{manifests.Count:N0} files  •  " + $"{totalChunks:N0} chunks  •  " +
                                     $"{FormatBytes(totalBytes)}  •  " +
                                     $"{stopwatch.Elapsed:hh\\:mm\\:ss}";

            progress.Value = 1000;
        }
        catch (Exception ex)
        {
            status.Text = "Split failed";
            details.Text = ex.Message;

            ShowError(ex.ToString(), "Split failed");
        }
        finally
        {
            Toggle(true);
        }
    }

    // ============================================================
    // ASSEMBLE
    // ============================================================

    private async Task AssembleAsync()
    {
        if (string.IsNullOrWhiteSpace(assemblePackage.Text) ||
            string.IsNullOrWhiteSpace(assembleDestination.Text))
        {
            ShowInformation("Select a package folder and destination file.", "Missing information");

            return;
        }

        if (!Directory.Exists(assemblePackage.Text))
        {
            ShowError("The selected package folder does not exist.", "Invalid package");

            return;
        }

        string? originalFileName = null;

        if (assemblePackageFiles.Count > 1)
        {
            int selectedIndex = assembleFilesList.SelectedIndex;

            if (selectedIndex < 0 || selectedIndex >= assemblePackageFiles.Count)
            {
                ShowInformation("Select which file to assemble from the list, " +
                                    "or use \"Assemble All Files\".",
                                "Select a file");

                return;
            }

            originalFileName = assemblePackageFiles[selectedIndex].OriginalFileName;
        }

        Toggle(false);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var progressReporter = new Progress<TransferProgress>(UpdateProgress);

            await new FileAssembler().AssembleAsync(assemblePackage.Text, assembleDestination.Text,
                                                    new FileTransportOptions(), progressReporter,
                                                    originalFileName: originalFileName);

            status.Text = "Assembly complete";

            details.Text =
                $"Created {assembleDestination.Text}  •  " + $"{stopwatch.Elapsed:hh\\:mm\\:ss}";

            progress.Value = 1000;
        }
        catch (Exception ex)
        {
            status.Text = "Assembly failed";
            details.Text = ex.Message;

            ShowError(ex.ToString(), "Assembly failed");
        }
        finally
        {
            Toggle(true);
        }
    }

    // ============================================================
    // ASSEMBLE ALL
    // ============================================================

    private async Task AssembleAllAsync()
    {
        if (string.IsNullOrWhiteSpace(assemblePackage.Text))
        {
            ShowInformation("Select a package folder.", "Missing information");

            return;
        }

        if (!Directory.Exists(assemblePackage.Text))
        {
            ShowError("The selected package folder does not exist.", "Invalid package");

            return;
        }

        using var dialog =
            new FolderBrowserDialog
            {
                Description =
                                          "Select a destination folder for the assembled files",

                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        Toggle(false);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var progressReporter = new Progress<MultiFileTransferProgress>(UpdateMultiProgress);

            var written = await new FileAssembler().AssembleAllAsync(
                assemblePackage.Text, dialog.SelectedPath, new FileTransportOptions(), progressReporter);

            status.Text = "Assembly complete";

            details.Text = $"Restored {written.Count:N0} file(s) to " + $"{dialog.SelectedPath}  •  " +
                           $"{stopwatch.Elapsed:hh\\:mm\\:ss}";

            progress.Value = 1000;
        }
        catch (Exception ex)
        {
            status.Text = "Assembly failed";
            details.Text = ex.Message;

            ShowError(ex.ToString(), "Assembly failed");
        }
        finally
        {
            Toggle(true);
        }
    }

    // ============================================================
    // PROGRESS
    // ============================================================

    private void UpdateProgress(TransferProgress transfer)
    {
        progress.Value = Math.Clamp((int)(transfer.Percent * 10), 0, 1000);

        status.Text = $"Processing   •   {transfer.Percent:0.0}%";

        details.Text = $"{FormatBytes(transfer.ProcessedBytes)} / " +
                       $"{FormatBytes(transfer.TotalBytes)}   •   " +
                       $"{transfer.CompletedChunks:N0} chunks";
    }

    private void UpdateMultiProgress(MultiFileTransferProgress transfer)
    {
        progress.Value = Math.Clamp((int)(transfer.FileProgress.Percent * 10), 0, 1000);

        status.Text = $"Processing file " + $"{transfer.FileIndex + 1}/" +
                      $"{transfer.FileCount}   •   " + $"{transfer.CurrentFileName}   •   " +
                      $"{transfer.FileProgress.Percent:0.0}%";

        details.Text = $"{FormatBytes(transfer.FileProgress.ProcessedBytes)} / " +
                       $"{FormatBytes(transfer.FileProgress.TotalBytes)}   •   " +
                       $"{transfer.FileProgress.CompletedChunks:N0} chunks";
    }

    // ============================================================
    // ENABLE / DISABLE
    // ============================================================

    private void Toggle(bool enabled)
    {
        splitButton.Enabled = enabled;
        assembleButton.Enabled = enabled;
        assembleAllButton.Enabled = enabled;

        tabs.Enabled = enabled;

        Cursor = enabled ? Cursors.Default : Cursors.WaitCursor;

        if (enabled)
        {
            progress.Value = 0;
        }
    }

    // ============================================================
    // MESSAGE BOX HELPERS
    // ============================================================

    private void ShowInformation(string message, string title)
    {
        MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ShowError(string message, string title)
    {
        MessageBox.Show(this, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    // ============================================================
    // FORMATTING
    // ============================================================

    private static string FormatBytes(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };

        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    // ============================================================
    // CONTROLS
    // ============================================================

    private TextBox CreateTextBox()
    {
        var textbox =
            new TextBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Surface2,
                ForeColor = Foreground,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
                Margin = new Padding(0),
                MinimumSize = DpiSize(0, 34),
                UseSystemPasswordChar = false
            };

        textbox.GotFocus += (_, _) => { textbox.BackColor = SurfaceHover; };

        textbox.LostFocus += (_, _) => { textbox.BackColor = Surface2; };

        return textbox;
    }

    private Button CreatePrimaryButton(string text)
    {
        var button = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = Accent,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold,
                                                  GraphicsUnit.Point),
            Cursor = Cursors.Hand,
            MinimumSize = DpiSize(180, 42),
            UseCompatibleTextRendering = false,
            TabStop = true
        };

        button.FlatAppearance.BorderSize = 0;

        button.FlatAppearance.MouseOverBackColor = AccentHover;

        button.FlatAppearance.MouseDownBackColor = AccentActive;

        button.MouseEnter += (_, _) => {
            if (button.Enabled)
            {
                button.BackColor = AccentHover;
            }
        };

        button.MouseLeave += (_, _) => {
            if (button.Enabled)
            {
                button.BackColor = Accent;
            }
        };

        button.MouseDown += (_, _) => {
            if (button.Enabled)
            {
                button.BackColor = AccentActive;
            }
        };

        button.MouseUp += (_, _) => {
            if (!button.Enabled)
            {
                return;
            }

            button.BackColor = button.ClientRectangle.Contains(button.PointToClient(Cursor.Position))
                                   ? AccentHover
                                   : Accent;
        };

        return button;
    }

    private Button CreateSecondaryButton(string text)
    {
        var button =
            new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = Surface2,
                ForeColor = Foreground,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Cursor = Cursors.Hand,
                MinimumSize = DpiSize(90, 34),
                UseCompatibleTextRendering = false
            };

        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.BorderSize = 1;

        button.FlatAppearance.MouseOverBackColor = SurfaceHover;

        button.MouseEnter += (_, _) => {
            if (!button.Enabled)
            {
                return;
            }

            button.BackColor = SurfaceHover;
            button.FlatAppearance.BorderColor = BorderLight;
        };

        button.MouseLeave += (_, _) => {
            if (!button.Enabled)
            {
                return;
            }

            button.BackColor = Surface2;
            button.FlatAppearance.BorderColor = Border;
        };

        return button;
    }

    private void StyleNumeric(NumericUpDown control)
    {
        control.BackColor = Surface2;
        control.ForeColor = Foreground;
        control.BorderStyle = BorderStyle.FixedSingle;

        control.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);

        control.TextAlign = HorizontalAlignment.Left;
    }
}