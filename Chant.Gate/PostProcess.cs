using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chant.Gate;

public partial class Gate
{
    private string PostProcess(string input)
    {
        // 動詞リストにある文字が登場したとき、そのあとには必ず'。'が来る。
        // OCRが見失うことがあるので、'。'がなかった場合は挿入する
        foreach (var verb in yukichantData.Verbs)
        {
            var index = input.IndexOf(verb, StringComparison.CurrentCulture);
            if (index < 0)
                continue;
            var maybeEndCharPosition = index + verb.Length;
            if (maybeEndCharPosition < input.Length && input[maybeEndCharPosition] != '。')
            {
                input = input.Insert(maybeEndCharPosition, "。");
            }
        }

        return input;
    }
}
