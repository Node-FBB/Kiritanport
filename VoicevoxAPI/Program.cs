// See https://aka.ms/new-console-template for more information

using System.IO.MemoryMappedFiles;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VoicevoxAPI;

Console.WriteLine("version>0.0.0");
Console.WriteLine("Initializing ...");

try
{
    await VoicevoxEngine.Init();
}
catch (Exception e)
{
    //HTTPサーバーに接続失敗するとおおむねここに飛ぶ
    Console.WriteLine($"error>{e.Message}");
    Console.WriteLine("Exit.");
    return;
}

Console.WriteLine("Ready.");
CancellationTokenSource? ctsource = null;

while (Console.ReadLine() is string read_line)
{
    //コマンド部分を切り抜き（※VOICEVOX APIと直接的な関係はない）
    //コマンドの入力は
    //[command]>[arguments]
    //出力は
    //[command]<[result]
    //
    //例）speech < こんにちは

    string cmd = read_line.Split('<')[0];

    switch (cmd)
    {
        case "exit":
            //プロセスを終了する

            Console.WriteLine("Exit.");

            return;

        case "clear":
            //メモリ上の音声ファイルを破棄する（ファイル名を指定するか、allですべての参照を対象にできる）
            //メモリ上のファイルが消去されるかどうかはGCが決めるため、即座に解放されるとは限らない

            string mmf_name = read_line["clear<".Length..];
            VoicevoxEngine.ClearMMFile(mmf_name);

            break;

        case "kana":
            //VOICEVOX APIを用いて読み仮名を生成する
            //出力はAIkana（VOICEROID等のAITalkで用いる仮名）と AqKana（AquesTalkで用いる仮名）の両方
            try
            {
                string text = read_line["kana<".Length..];
                VoicevoxEngine.TextToKana(text);
            }
            catch (Exception e)
            {
                Console.WriteLine($"error>{e.Message}");
            }
            break;

        case "speech":
            //VOICEVOX APIを用いて音声を生成する
            //出力はMemoryMappedFileに格納する
            //MemoryMappedFileはclearコマンドを使う（あるいはプロセスが終了する）まで参照を保持するので
            //連続で音声を生成した場合、メモリ使用量が大きくなる可能性があるので注意
            //平文またはAIKanaによる生成が可能
            //AqKanaによる生成は未実装（必要に応じて書き加えてください）
            try
            {
                string text = read_line["speech<".Length..];

                if (ctsource is not null)
                {
                    ctsource.Cancel();
                }
                ctsource = new CancellationTokenSource();
                VoicevoxEngine.TextToSpeech(text, ctsource.Token);
                //VoicevoxEngine.TextToSpeech(text);
            }
            catch (Exception e)
            {
                Console.WriteLine($"error>{e.Message}");
            }
            break;
        case "cancel":
            ctsource?.Cancel();
            ctsource = null;
            break;

        case "param":
            //話者パラメータを設定する
            //設定値はVOICEROIDを基準としている
            //
            // VOICEROIDパラメータ指定可能範囲
            // [Vol : 0.0 - 2.0]
            // [Spd : 0.5 - 4.0]
            // [Pit : 0.5 - 2.0]
            // [EMPH: 0.5 - 2.0]
            //
            //VOICEVOXでは指定可能なパラメータ範囲に明確な制約はないものの
            //この範囲に収まるようにした方が無難だと思われる

            string param_str = read_line["param<".Length..];
            VoicevoxEngine.SetParam(param_str);
            break;
    }
}
