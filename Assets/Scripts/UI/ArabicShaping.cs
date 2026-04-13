using System.Collections.Generic;
using System.Text;

namespace AURAID.UI
{
    /// <summary>Converts Arabic text (U+0600–U+06FF) to presentation forms (U+FE70–U+FEFF) so letters connect in TextMeshPro.</summary>
    public static class ArabicShaping
    {
        // Joining type: 0 = non-joining, 1 = right (connects to previous), 2 = left (connects to next), 3 = both
        const int U = 0, R = 1, L = 2, D = 3;

        // For each character: (isolated, final, initial, medial) as Unicode code points in FE70–FEFF, or 0 if not used
        struct Forms { public int iso, fin, ini, med; }
        static readonly Dictionary<char, Forms> Map = new Dictionary<char, Forms>
        {
            { '\u0627', new Forms { iso = 0xFE8D, fin = 0xFE8E, ini = 0, med = 0 } },   // ALEF
            { '\u0628', new Forms { iso = 0xFE8F, fin = 0xFE90, ini = 0xFE91, med = 0xFE92 } }, // BEH
            // TEH MARBUTA: Unicode only defines isolated (FE93) + final (FE94); no initial/medial code points.
            { '\u0629', new Forms { iso = 0xFE93, fin = 0xFE94, ini = 0, med = 0 } }, // ة
            { '\u062A', new Forms { iso = 0xFE95, fin = 0xFE96, ini = 0xFE97, med = 0xFE98 } }, // TEH
            { '\u062B', new Forms { iso = 0xFE99, fin = 0xFE9A, ini = 0xFE9B, med = 0xFE9C } }, // THEH
            { '\u062C', new Forms { iso = 0xFE9D, fin = 0xFE9E, ini = 0xFE9F, med = 0xFEA0 } }, // JEEM
            { '\u062D', new Forms { iso = 0xFEA1, fin = 0xFEA2, ini = 0xFEA3, med = 0xFEA4 } }, // HAH
            { '\u062E', new Forms { iso = 0xFEA5, fin = 0xFEA6, ini = 0xFEA7, med = 0xFEA8 } }, // KHAH
            { '\u062F', new Forms { iso = 0xFEA9, fin = 0xFEAA, ini = 0, med = 0 } },   // DAL
            { '\u0630', new Forms { iso = 0xFEAB, fin = 0xFEAC, ini = 0, med = 0 } },   // THAL
            { '\u0631', new Forms { iso = 0xFEAD, fin = 0xFEAE, ini = 0, med = 0 } },   // REH
            { '\u0632', new Forms { iso = 0xFEAF, fin = 0xFEB0, ini = 0, med = 0 } },   // ZAIN
            { '\u0633', new Forms { iso = 0xFEB1, fin = 0xFEB2, ini = 0xFEB3, med = 0xFEB4 } }, // SEEN
            { '\u0634', new Forms { iso = 0xFEB5, fin = 0xFEB6, ini = 0xFEB7, med = 0xFEB8 } }, // SHEEN
            { '\u0635', new Forms { iso = 0xFEB9, fin = 0xFEBA, ini = 0xFEBB, med = 0xFEBC } }, // SAD
            { '\u0636', new Forms { iso = 0xFEBD, fin = 0xFEBE, ini = 0xFEBF, med = 0xFEC0 } }, // DAD
            { '\u0637', new Forms { iso = 0xFEC1, fin = 0xFEC2, ini = 0xFEC3, med = 0xFEC4 } }, // TAH
            { '\u0638', new Forms { iso = 0xFEC5, fin = 0xFEC6, ini = 0xFEC7, med = 0xFEC8 } }, // ZAH
            { '\u0639', new Forms { iso = 0xFEC9, fin = 0xFECA, ini = 0xFECB, med = 0xFECC } }, // AIN
            { '\u063A', new Forms { iso = 0xFECD, fin = 0xFECE, ini = 0xFECF, med = 0xFED0 } }, // GHAIN
            { '\u0641', new Forms { iso = 0xFED1, fin = 0xFED2, ini = 0xFED3, med = 0xFED4 } }, // FEH
            { '\u0642', new Forms { iso = 0xFED5, fin = 0xFED6, ini = 0xFED7, med = 0xFED8 } }, // QAF
            { '\u0643', new Forms { iso = 0xFED9, fin = 0xFEDA, ini = 0xFEDB, med = 0xFEDC } }, // KAF
            { '\u0644', new Forms { iso = 0xFEDD, fin = 0xFEDE, ini = 0xFEDF, med = 0xFEE0 } }, // LAM
            { '\u0645', new Forms { iso = 0xFEE1, fin = 0xFEE2, ini = 0xFEE3, med = 0xFEE4 } }, // MEEM
            { '\u0646', new Forms { iso = 0xFEE5, fin = 0xFEE6, ini = 0xFEE7, med = 0xFEE8 } }, // NOON
            { '\u0647', new Forms { iso = 0xFEE9, fin = 0xFEEA, ini = 0xFEEB, med = 0xFEEC } }, // HEH
            { '\u0648', new Forms { iso = 0xFEED, fin = 0xFEEE, ini = 0, med = 0 } },   // WAW
            { '\u064A', new Forms { iso = 0xFEF1, fin = 0xFEF2, ini = 0xFEF3, med = 0xFEF4 } }, // YEH
            { '\u0649', new Forms { iso = 0xFEEF, fin = 0xFEF0, ini = 0, med = 0 } },   // ALEF MAKSURA
            { '\u0626', new Forms { iso = 0xFE89, fin = 0xFE8A, ini = 0xFE8B, med = 0xFE8C } }, // YEH HAMZA
            { '\u0623', new Forms { iso = 0xFE83, fin = 0xFE84, ini = 0, med = 0 } },   // ALEF HAMZA
            { '\u0625', new Forms { iso = 0xFE87, fin = 0xFE88, ini = 0, med = 0 } },   // ALEF HAMZA BELOW
            { '\u0624', new Forms { iso = 0xFE85, fin = 0xFE86, ini = 0, med = 0 } },   // WAW HAMZA
            { '\u0640', new Forms { iso = 0x0640, fin = 0x0640, ini = 0x0640, med = 0x0640 } }, // TATWEEL (kashida)
        };

        static bool ConnectsToNext(char c)
        {
            if (!Map.TryGetValue(c, out var f)) return false;
            return f.ini != 0 || f.med != 0;
        }

        static bool ConnectsToPrevious(char c)
        {
            if (!Map.TryGetValue(c, out var f)) return false;
            return f.fin != 0 || f.med != 0;
        }

        /// <summary>Converts logical-order Arabic to presentation forms so letters connect. Font must include Unicode range FE70–FEFF.</summary>
        public static string Shape(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;
            var sb = new StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (!Map.TryGetValue(c, out var forms))
                {
                    sb.Append(c);
                    continue;
                }
                bool prev = i > 0 && ConnectsToPrevious(c) && ConnectsToNext(input[i - 1]);
                bool next = i < input.Length - 1 && ConnectsToNext(c) && ConnectsToPrevious(input[i + 1]);
                int code;
                if (prev && next) code = forms.med != 0 ? forms.med : forms.iso;
                else if (prev) code = forms.fin != 0 ? forms.fin : forms.iso;
                else if (next) code = forms.ini != 0 ? forms.ini : forms.iso;
                else code = forms.iso;
                sb.Append(char.ConvertFromUtf32(code));
            }
            // Do not replace isolated ALEF + initial LAM (FE8D+FEDF) with U+FEFB: FEFB encodes lam+alef
            // in "لا" order; "ال" (article) is alef+lam and must stay as two glyphs or it reads as "la".
            // Lam final + isolated alef (some shaped "لا" / variants):
            return sb.Replace("\uFEDE\uFE8D", "\uFEFB").ToString();
        }
    }
}
