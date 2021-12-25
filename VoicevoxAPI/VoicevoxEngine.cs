using Kiritanport.Voiceroid;
using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace Kiritanport.Voicevox
{
    /// <summary>
    /// VOICEVOX ENGINE (0.9.0) に対応
    /// https://voicevox.github.io/voicevox_engine/api/
    /// </summary>
    internal static class VoicevoxEngine
    {
        private readonly static AudioQuery audio_query = new()
        {
            prePhonemeLength = 0.0,
            postPhonemeLength = 0.0,
            outputSamplingRate = 44100,
        };

        private readonly static Dictionary<string, MemoryMappedFile> mmfiles = new();
        private static string voice = "0";

        //VOICEVOX HTTPサーバーに接続するためのクライアント
        private static HttpClient client = new()
        {
            BaseAddress = new Uri("http://localhost:50021/"),
        };

        /// <summary>
        /// 初期化
        /// </summary>
        public static async Task Init()
        {
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
                if (JsonSerializer.Deserialize(json_str, typeof(TSpeaker[])) is TSpeaker[] res)
                {
                    foreach (TSpeaker speaker in res)
                    {
                        if (speaker.styles is null)
                        {
                            continue;
                        }

                        foreach (TStyle style in speaker.styles)
                        {
                            VoicePreset preset = new()
                            {
                                VoiceName = $"{style.id}",
                                PresetName = $"{speaker.name}({style.name})",
                            };

                            Console.WriteLine($"voice>{JsonSerializer.Serialize(preset)}");
                        }
                    }
                }
            }
            catch (Exception)
            {
                //HTTPサーバーに接続失敗するとおおむねここに飛ぶ
                throw new("VOICEVOX API is not found.");
            }
        }
        public static void SetParam(string param_str)
        {
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

            if (JsonSerializer.Deserialize<VoicePreset>(param_str) is VoicePreset preset)
            {
                voice = preset.VoiceName;
                audio_query.volumeScale = preset.Volume;
                audio_query.speedScale = preset.Speed;
                audio_query.pitchScale = (preset.Pitch - 1.0) / 10.0;
                audio_query.intonationScale = preset.PitchRange;
            }
        }
        public static async void TextToKana(string text)
        {
            long id = DateTime.Now.Ticks;
            Console.WriteLine($"process<{id}");

            //VOICEVOX APIを用いて読み仮名を生成する
            //出力はAIkana（VOICEROID等のAITalkで用いる仮名）と AqKana（AquesTalkで用いる仮名）の両方

            //リクエスト用のクエリ文字列を生成
            Dictionary<string, string> parameters;

            parameters = new Dictionary<string, string>()
                {
                    { "speaker", voice },
                    { "text", text },
                    { "is_kana", "false" }
                };

            //VOICEVOX API ("http://localhost:50021/accent_phrases")にリクエストをPOSTする
            HttpResponseMessage response = await client.PostAsync($"accent_phrases?{await new FormUrlEncodedContent(parameters).ReadAsStringAsync()}", null);
            MemoryStream stream = new();
            response.Content.ReadAsStream().CopyTo(stream);
            string json = Encoding.UTF8.GetString(stream.ToArray());

            //アクセント句（AccentPhraseの配列）がJSON文字列で返ってくるのでデシリアライズする
            if (JsonSerializer.Deserialize(json, typeof(TAccentPhrase[])) is TAccentPhrase[] accent_phrases)
            {
                //音声合成用のクエリに取得したアクセント句を設定
                audio_query.accent_phrases = accent_phrases;

                if (audio_query.kana is null)
                {
                    throw new($"blank or illegal text.");
                }

                //アクセント句を各種読み仮名に変換して出力する
                Console.WriteLine($"aqkana>{audio_query.kana}");
                Console.WriteLine($"aikana>{KanaConvarter.AqKanaToAIKana(audio_query.kana)}");

                /*

                double tick = 0;//1tick = 100ns , 1s = 10,000,000tick
                foreach (var phrase in audio_query.accent_phrases)
                {
                    foreach (var mora in phrase.moras)
                    {

                        if (mora.consonant is not null && mora.consonant_length is not null)
                        {
                            Console.Write($"{tick} ");
                            tick += (long)(mora.consonant_length * 10000000L);
                            Console.WriteLine($"{tick} {mora.consonant}");
                        }

                        Console.Write($"{tick} ");
                        tick += (long)(mora.vowel_length * 10000000L);
                        Console.WriteLine($"{tick} {mora.vowel}");

                    }
                    if (phrase.pause_mora is not null)
                    {
                        if (phrase.pause_mora.consonant is not null && phrase.pause_mora.consonant_length is not null)
                        {
                            Console.Write($"{tick} ");
                            tick += (long)(phrase.pause_mora.consonant_length * 10000000L);
                            Console.WriteLine($"{tick} {phrase.pause_mora.consonant}");
                        }

                        Console.Write($"{tick} ");
                        tick += (long)(phrase.pause_mora.vowel_length * 10000000L);
                        Console.WriteLine($"{tick} {phrase.pause_mora.vowel}");
                    }
                }
                */
            }
            Console.WriteLine($"process>{id}");
        }
        public static void ClearMMFile(string mmf_name)
        {
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
        }
        public static async void TextToSpeech(string text)
        {
            long id = DateTime.Now.Ticks;
            Console.WriteLine($"process<{id}");

            //VOICEVOX APIを用いて音声を生成する
            //出力はMemoryMappedFileに格納する
            //MemoryMappedFileはclearコマンドを使う（あるいはプロセスが終了する）まで参照を保持するので
            //連続で音声を生成した場合、メモリ使用量が大きくなる可能性があるので注意
            //平文またはAIKanaによる生成が可能
            //AqKanaによる生成は未実装（必要に応じて書き加えてください）

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

            if (JsonSerializer.Deserialize(json, typeof(TAccentPhrase[])) is TAccentPhrase[] accent_phrases)
            {
                audio_query.accent_phrases = accent_phrases;

                if (audio_query.kana is null)
                {
                    throw new($"blank or illegal text.");
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
                HttpResponseMessage result = await client.PostAsync($"synthesis?{await new FormUrlEncodedContent(parameters).ReadAsStringAsync()}", content);

                //生成結果をMemoryStreamへコピー
                MemoryStream wavData = new();
                result.Content.ReadAsStream().CopyTo(wavData);

                //適当な名前を付けてMemoryMappedFileを作成
                string name = $"voicevox_{id}";
                MemoryMappedFile mmf = MemoryMappedFile.CreateNew(name, wavData.Length);
                mmf.CreateViewStream().Write(wavData.ToArray(), 0, (int)wavData.Length);
                mmfiles[name] = mmf;
                Console.WriteLine($"wave>{name}");
            }
            Console.WriteLine($"process>{id}");
        }
        public static async void TextToSpeech(string text, CancellationToken ct)
        {
            long id = DateTime.Now.Ticks;
            Console.WriteLine($"process<{id}");

            //VOICEVOX APIを用いて音声を生成する
            //出力はMemoryMappedFileに格納する
            //MemoryMappedFileはclearコマンドを使う（あるいはプロセスが終了する）まで参照を保持するので
            //連続で音声を生成した場合、メモリ使用量が大きくなる可能性があるので注意
            //平文またはAIKanaによる生成が可能
            //AqKanaによる生成は未実装（必要に応じて書き加えてください）

            try
            {
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

                HttpResponseMessage? response = await client.PostAsync($"accent_phrases?{await new FormUrlEncodedContent(parameters).ReadAsStringAsync(ct)}", null, ct);

                MemoryStream stream = new();
                response.Content.ReadAsStream(ct).CopyTo(stream);
                string json = Encoding.UTF8.GetString(stream.ToArray());

                if (JsonSerializer.Deserialize(json, typeof(TAccentPhrase[])) is TAccentPhrase[] accent_phrases)
                {
                    audio_query.accent_phrases = accent_phrases;

                    if (audio_query.kana is null)
                    {
                        throw new($"blank or illegal text.");
                    }

                    Console.WriteLine($"aqkana>{audio_query.kana}");
                    Console.WriteLine($"aikana>{KanaConvarter.AqKanaToAIKana(audio_query.kana)}");

                    /*
                     * .labファイル読み込みテスト
                     * 
                    using StreamReader reader = new(@".\test.lab");

                    List<(double t, string s)> lab = new();
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Split(" ").Length == 3)
                        {
                            double time = (long.Parse(line.Split(" ")[1]) - long.Parse(line.Split(" ")[0])) / 10000000.0;

                            string s = line.Split(" ")[2];

                            lab.Add((time, s));
                        }
                    }

                    int cnt = 0;

                    foreach (var phrase in audio_query.accent_phrases)
                    {
                        foreach (var mora in phrase.moras)
                        {
                            if (mora.consonant is not null && mora.consonant_length is not null)
                            {
                                Console.WriteLine($"{mora.consonant}[{mora.consonant_length}]:{lab[cnt].s}[{lab[cnt].t}]");
                                mora.consonant_length = lab[cnt].t;
                                cnt++;
                            }

                            Console.WriteLine($"{mora.vowel}[{mora.vowel_length}]:{lab[cnt].s}[{lab[cnt].t}]");
                            mora.vowel_length = lab[cnt].t;
                            cnt++;
                        }
                        if (phrase.pause_mora is not null)
                        {
                            if (phrase.pause_mora.consonant is not null && phrase.pause_mora.consonant_length is not null)
                            {
                                Console.WriteLine($"{phrase.pause_mora.consonant}[{phrase.pause_mora.consonant_length}]:{lab[cnt].s}[{lab[cnt].t}]");
                                phrase.pause_mora.consonant_length = lab[cnt].t;
                                cnt++;
                            }

                            Console.WriteLine($"{phrase.pause_mora.vowel}[{phrase.pause_mora.vowel_length}]:{lab[cnt].s}[{lab[cnt].t}]");
                            phrase.pause_mora.vowel_length = lab[cnt].t;
                            cnt++;
                        }
                    }

                    */

                    //音声合成用のクエリをHttpContentに格納
                    HttpContent content = new StringContent(JsonSerializer.Serialize(audio_query), Encoding.UTF8, @"application/json");

                    parameters = new Dictionary<string, string>
                    {
                        { "speaker", voice }
                    };
                    //VOICEVOX API ("http://localhost:50021/synthesis")にリクエストをPOSTする
                    //cancellable_synthesisはデフォルトで無効？（現時点でWIPなので、完成するまでsynthesisを使用する）
                    //HttpResponseMessage result = await client.PostAsync($"cancellable_synthesis?{await new FormUrlEncodedContent(parameters).ReadAsStringAsync(ct)}", content, ct);
                    HttpResponseMessage result = await client.PostAsync($"synthesis?{await new FormUrlEncodedContent(parameters).ReadAsStringAsync(ct)}", content, ct);

                    //生成結果をMemoryStreamへコピー
                    MemoryStream wavData = new();
                    result.Content.ReadAsStream(ct).CopyTo(wavData);

                    using BinaryWriter writer = new(new FileStream(@".\test2.wav", FileMode.OpenOrCreate));
                    writer.Write(wavData.ToArray());

                    //適当な名前を付けてMemoryMappedFileを作成
                    string name = $"voicevox_{id}";
                    MemoryMappedFile mmf = MemoryMappedFile.CreateNew(name, wavData.Length);
                    mmf.CreateViewStream().Write(wavData.ToArray(), 0, (int)wavData.Length);
                    mmfiles[name] = mmf;
                    Console.WriteLine($"wave>{name}");
                }
                Console.WriteLine($"process>{id}");
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine($"process>{id}");
            }
        }
    }
}
