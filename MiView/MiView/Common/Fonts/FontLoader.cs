using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiView.Common.Fonts
{
    internal class FontLoader
    {
        /// <summary>
        /// ふぉんとコレクション
        /// </summary>
        private PrivateFontCollection _Fonts = new PrivateFontCollection();

        /// <summary>
        /// フォント格納先基準ディレクトリ
        /// </summary>
        private const string _FontDirectory = @"./Common/Fonts";

        private const string _Font_Prefix = "_Font_";

        /// <summary>
        /// フォント格納先/MaterialIcon
        /// </summary>
        private const string _Font_MaterialIcons = @"/Material/MaterialIcons-Regular.ttf";

        /// <summary>
        /// フォント名と定数のペア
        /// </summary>
        private readonly Dictionary<FONT_SELECTOR, string> _FontPair = new Dictionary<FONT_SELECTOR, string>()
        {
            { FONT_SELECTOR.MATERIALICONS, _Font_MaterialIcons },
        };

        /// <summary>
        /// 読み込みフォント指定
        /// </summary>
        public enum FONT_SELECTOR
        {
            UNSELECT = -1,
            MATERIALICONS = 0
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public FontLoader()
        {
        }

        /// <summary>
        /// フォントデータをファイルから取得
        /// </summary>
        /// <param name="Selector">フォント指定</param>
        /// <param name="Size">フォントサイズ</param>
        /// <returns>Font</returns>
        /// <exception cref="KeyNotFoundException">対応するフォントがない</exception>
        public Font LoadFontFromFile(FONT_SELECTOR Selector, float Size)
        {
            if (!this._FontPair.ContainsKey(Selector))
            {
                throw new KeyNotFoundException();
            }

            System.Drawing.Text.PrivateFontCollection Col = new System.Drawing.Text.PrivateFontCollection();
            Col.AddFontFile(_Font_Prefix + _FontPair[Selector]);

            return new System.Drawing.Font(Col.Families[0], Size);
        }
    }
}
