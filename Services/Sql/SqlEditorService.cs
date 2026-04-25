using System;
using System.Drawing;
using ScintillaNET;
using ScintillaNET.WPF;

namespace sqlSense.Services
{
    public class SqlEditorService
    {
        public const string SqlKeywords1 =
            "add all alter and any as asc authorization backup begin between break browse bulk by cascade case check checkpoint close clustered coalesce collate column commit compute constraint contains containstable continue convert create cross current current_date current_time current_timestamp current_user cursor database dbcc deallocate declare default delete deny desc disk distinct distributed double drop dump else end errlvl escape except exec execute exists exit external fetch file fillfactor for foreign freetext freetexttable from full function goto grant group having holdlock identity identitycol identity_insert if in index inner insert intersect into is join key kill left like lineno load merge national nocheck nonclustered not null nullif of off offsets on open opendatasource openquery openrowset openxml option or order outer over percent pivot plan precision primary print proc procedure public raiserror read readtext reconfigure references replication restore restrict return revert revoke right rollback rowcount rowguidcol rule save schema securityaudit select semantickeyphrasetable semanticsimilaritydetailstable semanticsimilaritytable session_user set setuser shutdown some statistics system_user table tablesample textsize then to top tran transaction trigger truncate try_convert tsequal union unique unpivot update updatetext use user values varying view waitfor when where while with within group writetext";

        public const string SqlKeywords2 =
            "abs ascii cast ceiling char charindex coalesce concat convert count datename datepart dateadd datediff day getdate getutcdate iif isnull isdate isnumeric left len lower ltrim max min month newid newsequentialid nullif object_id object_name parsename patindex replace right rtrim scope_identity sign space sqrt str stuff substring sum sysdatetime sysutcdatetime trim upper year avg count_big stdev stdevp var varp row_number rank dense_rank ntile lag lead first_value last_value percent_rank cume_dist";

        public static Bitmap CreateColorIcon(Color color, string letter)
        {
            var bmp = new Bitmap(16, 16);
            using var g = Graphics.FromImage(bmp);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 1, 1, 14, 14);
            using var textBrush = new SolidBrush(Color.White);
            using var font = new Font("Segoe UI", 8, FontStyle.Bold);
            
            var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(letter, font, textBrush, new RectangleF(1, 1, 14, 14), format);
            return bmp;
        }

        public static void Configure(ScintillaWPF editor)
        {
            // ─── General ──────────────────────────────────────────────
            editor.WrapMode = WrapMode.None;
            editor.IndentationGuides = IndentView.LookBoth;
            editor.ScrollWidthTracking = true;
            editor.ScrollWidth = 1;
            editor.AutoCIgnoreCase = true;
            editor.AutoCDropRestOfWord = false;
            editor.CaretForeColor = System.Windows.Media.Color.FromRgb(174, 175, 173);
            editor.CaretWidth = 2;

            // ─── AutoComplete Popup Theming (via DirectMessage) ────────
            editor.AutoCOrder = Order.Custom;
            editor.AutoCSeparator = '|';
            editor.AutoCMaxHeight = 10;
            editor.AutoCMaxWidth = 50;

            editor.DirectMessage(2205, new IntPtr(ColorTranslator.ToWin32(Color.FromArgb(37, 37, 38))), IntPtr.Zero);
            editor.DirectMessage(2206, new IntPtr(ColorTranslator.ToWin32(Color.FromArgb(212, 212, 212))), IntPtr.Zero);
            editor.DirectMessage(2208, new IntPtr(ColorTranslator.ToWin32(Color.FromArgb(9, 71, 113))), IntPtr.Zero);
            editor.DirectMessage(2209, new IntPtr(ColorTranslator.ToWin32(Color.White)), IntPtr.Zero);

            // Register AutoComplete Icons
            editor.RegisterRgbaImage(1, CreateColorIcon(Color.Gray, "D"));
            editor.RegisterRgbaImage(2, CreateColorIcon(Color.Goldenrod, "S"));
            editor.RegisterRgbaImage(3, CreateColorIcon(Color.CornflowerBlue, "T"));
            editor.RegisterRgbaImage(4, CreateColorIcon(Color.MediumSeaGreen, "V"));
            editor.RegisterRgbaImage(5, CreateColorIcon(Color.MediumPurple, "P"));

            // ─── Margins ────────────────────────────────────────────
            editor.Margins[0].Width = 44;
            editor.Margins[0].Type = MarginType.Number;
            editor.Margins[1].Width = 0;
            editor.Margins[2].Type = MarginType.Symbol;
            editor.Margins[2].Mask = Marker.MaskFolders;
            editor.Margins[2].Sensitive = true;
            editor.Margins[2].Width = 18;

            // ─── Lexer ──────────────────────────────────────────────
            editor.Lexer = Lexer.Sql;

            // ─── Default Style ──────────────────────────────────────
            editor.Styles[ScintillaNET.Style.Default].Font = "Consolas";
            editor.Styles[ScintillaNET.Style.Default].Size = 13;
            editor.Styles[ScintillaNET.Style.Default].BackColor = Color.FromArgb(30, 30, 30);
            editor.Styles[ScintillaNET.Style.Default].ForeColor = Color.FromArgb(212, 212, 212);
            editor.StyleClearAll();

            // ─── SQL Syntax Colors (VS Code Dark+ palette) ───────────
            editor.Styles[0].ForeColor = Color.FromArgb(212, 212, 212);
            editor.Styles[1].ForeColor = Color.FromArgb(106, 153, 85); editor.Styles[1].Italic = true;
            editor.Styles[2].ForeColor = Color.FromArgb(106, 153, 85); editor.Styles[2].Italic = true;
            editor.Styles[3].ForeColor = Color.FromArgb(106, 153, 85);
            editor.Styles[4].ForeColor = Color.FromArgb(181, 206, 168);
            editor.Styles[5].ForeColor = Color.FromArgb(86, 156, 214); editor.Styles[5].Bold = true;
            editor.Styles[6].ForeColor = Color.FromArgb(206, 145, 120);
            editor.Styles[7].ForeColor = Color.FromArgb(206, 145, 120);
            editor.Styles[10].ForeColor = Color.FromArgb(212, 212, 212);
            editor.Styles[11].ForeColor = Color.FromArgb(212, 212, 212);
            editor.Styles[12].ForeColor = Color.FromArgb(156, 220, 254);
            editor.Styles[14].ForeColor = Color.FromArgb(156, 220, 254);
            editor.Styles[15].ForeColor = Color.FromArgb(156, 220, 254);
            editor.Styles[16].ForeColor = Color.FromArgb(220, 220, 170);

            foreach (var s in editor.Styles) s.BackColor = Color.FromArgb(30, 30, 30);

            // ─── Line Number Margin Style ────────────────────────────
            editor.Styles[ScintillaNET.Style.LineNumber].BackColor = Color.FromArgb(30, 30, 30);
            editor.Styles[ScintillaNET.Style.LineNumber].ForeColor = Color.FromArgb(133, 133, 133);

            // ─── Fold Margin ─────────────────────────────────────────
            editor.SetFoldMarginColor(true, System.Windows.Media.Color.FromArgb(255, 30, 30, 30));
            editor.SetFoldMarginHighlightColor(true, System.Windows.Media.Color.FromArgb(255, 30, 30, 30));

            // ─── Fold Markers ────────────────────────────────────────
            editor.Markers[Marker.FolderEnd].Symbol = MarkerSymbol.BoxPlusConnected;
            editor.Markers[Marker.FolderOpenMid].Symbol = MarkerSymbol.BoxMinusConnected;
            editor.Markers[Marker.FolderMidTail].Symbol = MarkerSymbol.TCorner;
            editor.Markers[Marker.FolderTail].Symbol = MarkerSymbol.LCorner;
            editor.Markers[Marker.FolderSub].Symbol = MarkerSymbol.VLine;
            editor.Markers[Marker.Folder].Symbol = MarkerSymbol.BoxPlus;
            editor.Markers[Marker.FolderOpen].Symbol = MarkerSymbol.BoxMinus;

            for (int i = Marker.FolderEnd; i <= Marker.FolderOpen; i++)
            {
                editor.Markers[i].SetForeColor(Color.FromArgb(30, 30, 30));
                editor.Markers[i].SetBackColor(Color.FromArgb(100, 100, 100));
            }

            // ─── Folding ─────────────────────────────────────────────
            editor.SetProperty("fold", "1");
            editor.SetProperty("fold.compact", "0");
            editor.SetProperty("fold.comment", "1");
            editor.SetProperty("fold.sql.only.begin", "1");

            // ─── Indentation ─────────────────────────────────────────
            editor.IndentWidth = 4;
            editor.UseTabs = false;
            editor.TabWidth = 4;

            // ─── Selection ───────────────────────────────────────────
            editor.SetSelectionBackColor(true, System.Windows.Media.Color.FromArgb(255, 38, 79, 120));

            // ─── Current Line Highlight ───────────────────────────────
            editor.CaretLineVisible = true;
            editor.CaretLineBackColor = System.Windows.Media.Color.FromArgb(255, 40, 40, 40);

            // ─── Keyword Registration ──────────────────────────────────
            editor.SetKeywords(0, SqlKeywords1.ToLower());
            editor.SetKeywords(1, SqlKeywords2.ToLower());
        }
    }
}
