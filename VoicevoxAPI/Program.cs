// See https://aka.ms/new-console-template for more information

using System.IO.MemoryMappedFiles;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VoicevoxAPI;

Console.WriteLine("version>0.0.0");
Console.WriteLine("Initializing ...");

//VOICEVOX HTTPサーバーに接続するためのクライアント
HttpClient client = new()
{
    BaseAddress = new Uri("http://localhost:50021/"),
};

client.DefaultRequestHeaders.Accept.Clear();
client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

try
{
    //話者リストの取得
    HttpResponseMessage response = await client.GetAsync($"speakers");

    //結果をMemoryStreamにコピーしてJSON文字列へ変換
    MemoryStream stream = new();
    response.Content.ReadAsStream().CopyTo(stream);
    string json_str = Encoding.UTF8.GetString(stream.ToArray());

    //JSON文字列から話者リストへデシリアライズする
    if (JsonSerializer.Deserialize(json_str, typeof(Speaker[])) is Speaker[] res)
    {
        foreach (Speaker speaker in res)
        {
            if (speaker.styles is null)
            {
                continue;
            }

            foreach (Style style in speaker.styles)
            {
                Console.WriteLine($"voice>{style.id}:{speaker.name}({style.name})");
            }
        }
    }
}
catch (Exception)
{
    //HTTPサーバーに接続失敗するとおおむねここに飛ぶ
    Console.WriteLine("error>VOICEVOX API is not found.");
    Console.WriteLine("Exit.");
    return;
}

Console.WriteLine("Ready.");

//音声合成用のクエリと音声格納用のMemoryMappedFile（の参照置き場）を用意
AudioQuery audio_query = new();
Dictionary<string, MemoryMappedFile> mmfiles = new();

//話者を指定するID　0 は 四国めたん（あまあま）を指す
string voice = "0";

while (Console.ReadLine() is string read_line)
{
    //開始時の時刻をプロセスIDとする（※VOICEVOX APIと直接的な関係はない）
    //要求したタスクが完了したかどうかを調べるための物
    long id = DateTime.Now.Ticks;
    Console.WriteLine($"process<{id}:{read_line}");

    //コマンド部分を切り抜き（※VOICEVOX APIと直接的な関係はない）
    //コマンドの入力は
    //[command]>[arguments]
    //出力は
    //[command]<[result]
    //
    //例）speech < こんにちは

    string cmd = read_line[..read_line.IndexOf('>')];

    switch (cmd)
    {
        case "exit":
            //プロセスを終了する

            Console.WriteLine("Exit.");

            return;

        case "clear":
            //メモリ上の音声ファイルの消去する（ファイル名を指定するかallですべて）
            //ファイルが消去されるかどうかはGCが決めるため、正確には参照を破棄するだけ
            //別プロセスで指定したMemoryMappedFileを参照している状態でこのコマンドを使わないように

            string mmf_name = read_line["clear<".Length..];

            if (mmf_name == "all")
            {
                foreach (MemoryMappedFile mmfile in mmfiles.Values)
                {
                    mmfile.Dispose();
                }
                mmfiles.Clear();
            }
            else
            {
                try
                {
                    mmfiles[mmf_name].Dispose();
                    mmfiles.Remove(mmf_name);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"error>{e.Message}");
                }
            }

            break;

        case "kana":
            //VOICEVOX APIを用いて読み仮名を生成する
            //出力はAIkana（VOICEROID等のAITalkで用いる仮名）と AqKana（AquesTalkで用いる仮名）の両方
            try
            {
                string text = read_line["kana<".Length..];

                //リクエスト用のクエリ文字列を生成
                Dictionary<string, string> parameters;

                parameters = new Dictionary<string, string>()
                {
                    { "speaker", voice },
                    { "text", text },
                    { "is_kana", "false" }
                };

                //VOICEVOX API ("http://localhost:50021/accent_phrases")にリクエストをPOSTする
                //API詳細（"https://voicevox.github.io/voicevox_engine/api/#tag/%E3%82%AF%E3%82%A8%E3%83%AA%E7%B7%A8%E9%9B%86"）
                HttpResponseMessage response = await client.PostAsync($"accent_phrases?{await new FormUrlEncodedContent(parameters).ReadAsStringAsync()}", null);
                MemoryStream stream = new();
                response.Content.ReadAsStream().CopyTo(stream);
                string json = Encoding.UTF8.GetString(stream.ToArray());

                //アクセント句（AccentPhraseの配列）がJSON文字列で返ってくるのでデシリアライズする
                if (JsonSerializer.Deserialize(json, typeof(AccentPhrase[])) is AccentPhrase[] accent_phrases)
                {
                    //音声合成用のクエリに取得したアクセント句を設定
                    audio_query.accent_phrases = accent_phrases;

                    if (audio_query.kana is null)
                    {
                        Console.WriteLine($"error>blank or illegal text.");
                        break;
                    }

                    //アクセント句を各種読み仮名に変換して出力する
                    Console.WriteLine($"aqkana>{audio_query.kana}");
                    Console.WriteLine($"aikana>{KanaConvarter.AqKanaToAIKana(audio_query.kana)}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
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

                Dictionary<string, string> parameters;

                if (text.StartsWith("<S>"))
                {
                    parameters = new Dictionary<string, string>()
                    {
                        { "speaker", voice },
                        { "text", KanaConvarter.AIKanaToAqKana(text) },
                        { "is_kana", "true" }
                    };
                }
                else
                {
                    parameters = new Dictionary<string, string>()
                    {
                        { "speaker", voice },
                        { "text", text },
                        { "is_kana", "false" }
                    };
                }

                HttpResponseMessage? response = await client.PostAsync($"accent_phrases?{await new FormUrlEncodedContent(parameters).ReadAsStringAsync()}", null);
                MemoryStream stream = new();
                response.Content.ReadAsStream().CopyTo(stream);
                string json = Encoding.UTF8.GetString(stream.ToArray());

                if (JsonSerializer.Deserialize(json, typeof(AccentPhrase[])) is AccentPhrase[] accent_phrases)
                {
                    audio_query.accent_phrases = accent_phrases;

                    if (audio_query.kana is null)
                    {
                        Console.WriteLine($"error>blank or illegal text.");
                        break;
                    }

                    Console.WriteLine($"aqkana>{audio_query.kana}");
                    Console.WriteLine($"aikana>{KanaConvarter.AqKanaToAIKana(audio_query.kana)}");

                    //音声合成用のクエリをHttpContentに格納
                    HttpContent content = new StringContent(JsonSerializer.Serialize(audio_query), Encoding.UTF8, @"application/json");

                    parameters = new Dictionary<string, string>
                    {
                        { "speaker", voice }
                    };

                    //VOICEVOX API ("http://localhost:50021/synthesis")にリクエストをPOSTする
                    //API詳細（"https://voicevox.github.io/voicevox_engine/api/#tag/%E9%9F%B3%E5%A3%B0%E5%90%88%E6%88%90"）
                    HttpResponseMessage result = await client.PostAsync($"synthesis?{await new FormUrlEncodedContent(parameters).ReadAsStringAsync()}", content);

                    //生成結果をMemoryStreamへコピー
                    MemoryStream wavData = new();
                    result.Content.ReadAsStream().CopyTo(wavData);

                    //適当な名前を付けてMemoryMappedFileを作成
                    string name = $"voicevox_{mmfiles.Count}";
                    MemoryMappedFile mmf = MemoryMappedFile.CreateNew(name, wavData.Length);
                    mmf.CreateViewStream().Write(wavData.ToArray(), 0, (int)wavData.Length);
                    mmfiles[name] = mmf;
                    Console.WriteLine($"wave>{name}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
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

            foreach (string str in param_str.Split(' '))
            {
                if (str.StartsWith("voice:"))
                {
                    voice = str.Split(':')[1];
                }
                if (str.StartsWith("vol:"))
                {
                    float volume = float.Parse(str.Split(':')[1]);
                    audio_query.volumeScale = volume;
                }
                if (str.StartsWith("spd:"))
                {
                    float speed = float.Parse(str.Split(':')[1]);
                    audio_query.speedScale = speed;
                }
                if (str.StartsWith("pit:"))
                {
                    float pitch = float.Parse(str.Split(':')[1]);
                    audio_query.pitchScale = (pitch - 1.0) / 10.0;
                }
                if (str.StartsWith("emph:"))
                {
                    float range = float.Parse(str.Split(':')[1]);
                    audio_query.intonationScale = range;
                }
            }
            break;
    }

    //タスク完了通知
    Console.WriteLine($"process>{id}:{read_line}");
}
