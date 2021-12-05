// See https://aka.ms/new-console-template for more information

using System.IO.MemoryMappedFiles;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VoicevoxAPI;

Console.WriteLine("version>0.0.0");
Console.WriteLine("Initializing ...");

HttpClient client = new()
{
    BaseAddress = new Uri("http://localhost:50021/"),
};

client.DefaultRequestHeaders.Accept.Clear();
client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

try
{
    var response = await client.GetAsync($"speakers");
    var stream = new MemoryStream();
    response.Content.ReadAsStream().CopyTo(stream);
    string json_str = Encoding.UTF8.GetString(stream.ToArray());

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
    Console.WriteLine("error>VOICEVOX API is not found.");
    Console.WriteLine("Exit.");
    return;
}

Console.WriteLine("Ready.");

AudioQuery audio_query = new();
Dictionary<string, MemoryMappedFile> mmfiles = new();

string voice = "1";

while (Console.ReadLine() is string read_line)
{
    long id = DateTime.Now.Ticks;
    Console.WriteLine($"process<{id}:{read_line}");

    string cmd = read_line[..read_line.IndexOf(read_line.First(c => c == '>' || c == '<'))];

    switch (cmd)
    {
        case "exit":

            Console.WriteLine("Exit.");

            return;

        case "clear":

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
                catch(Exception e)
                {
                    Console.WriteLine($"error>{e.Message}");
                }
            }

            break;

        case "kana":
            try
            {
                string text = read_line["kana<".Length..];

                Dictionary<string, string> parameters;

                parameters = new Dictionary<string, string>()
                {
                    { "speaker", voice },
                    { "text", text },
                    { "is_kana", "false" }
                };

                var response = await client.PostAsync($"accent_phrases?{await new FormUrlEncodedContent(parameters).ReadAsStringAsync()}", null);
                var stream = new MemoryStream();
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
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            break;

        case "speech":
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

                var response = await client.PostAsync($"accent_phrases?{await new FormUrlEncodedContent(parameters).ReadAsStringAsync()}", null);
                var stream = new MemoryStream();
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

                    HttpContent content = new StringContent(JsonSerializer.Serialize(audio_query), Encoding.UTF8, @"application/json");

                    parameters = new Dictionary<string, string>
                    {
                        { "speaker", voice }
                    };
                    var result = await client.PostAsync($"synthesis?{await new FormUrlEncodedContent(parameters).ReadAsStringAsync()}", content);

                    var wavData = new MemoryStream();
                    result.Content.ReadAsStream().CopyTo(wavData);

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
    Console.WriteLine($"process>{id++}:{read_line}");
}
