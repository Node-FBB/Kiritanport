# Kiritanport (きりたんぽーと)
## VOICEROID2、VOICEVOX　統合フレーズエディタ
### 注意事項
未完成です<br>
マニュアルを含むドキュメントも未整備です<br>
<br>
・本ツールの起動には[.NET Runtime 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)が必要です。<br>
・本ツールの動作には「VOICEROID2」「VOICEVOX」「AssistantSeika（及び対応している合成音声ソフト）」の内、最低でも1つ以上の外部ツールが必要です。<br>
・「VOICEROID2」は本ツール起動前及び起動中に「起動しない」でください。<br>
・「VOICEVOX」及び「AssistantSeika」は本ツール起動前に「起動しておいて」ください。<br>
・「VOICEROID2」のインストール場所をデフォルトから変更している場合、「settings.ini」ファイルの編集が必要です。<br>
・「フレーズ辞書」機能は「VOICEROID2」および「VOICEVOX」で利用可能です。<br>
・「単語辞書」機能の利用には「VOICEROID2」が必要です。
（VOICEVOXで単語辞書を利用する場合は、使用する辞書をDefaultからVoiceroidへ変更してください）<br>
・「単語辞書」機能を利用する場合、商用利用可能なVOICEVOX音声であっても商用利用することができなくなります。
（VOICEROID2の機能を利用することになるのでVOICEVOX、VOICEROID2両方のライセンスに従う必要があります）<br>
・「AssistantSeika」の設定で基本設定タブの「16bitのwavファイルを使用」及び「前後の無音部分削除」を選択してください。<br>
### 入手
https://github.com/Node-FBB/Kiritanport/releases
### 使い方
https://github.com/Node-FBB/Kiritanport/wiki
### 更新履歴
0.3.0　UIを.NET6に変更し、テスト版公開<br>
<br>
0.1.1　デスクトップマスコット機能追加（諸事情により公開停止）<br>
0.1.0　UI操作からAPI操作へ切替<br>
<br>
0.0.5　フレームワーク変更、バグ修正<br>
0.0.4　範囲指定読み上げ、単語辞書登録機能追加<br>
0.0.3　フレーズ辞書検索機能追加<br>
0.0.2　フレーズ調声機能追加<br>
0.0.1　公開<br>
### 外部ツール（必須）
本ツールの動作には以下3つのうち、少なくとも1つ以上の外部ツールが必要となります
#### [VOICEROID2](https://www.ah-soft.com)
VOICEROID音声の利用に必要（EXおよびEX+は不可、VOICEROID2にボイスライブラリをインポートすれば可）<br>
ver 2.0.1.0 では動作しないことが確認されています。ver 2.1.1.0 (32bit版) で動作確認済みです。
#### [VOICEVOX](https://voicevox.hiroshiba.jp)
VOICEVOX音声の利用に必要<br>
ver 0.1.1 では動作しないことが確認されています。ver 0.9.3 で動作確認済みです。
#### [AssistantSeika](https://hgotoh.jp/wiki/doku.php/start)
対応している多数の合成音声ソフトから音声を取得できます（アクセント等の変更はできません）<br>
ver 20211205/u で動作確認済みです。
### 外部ツール（連携）
#### [AviUtl](http://spring-fragrance.mints.ne.jp/aviutl/)
ver 1.1.0 で動作確認済み。
#### [PSDToolKit](https://oov.github.io/aviutl_psdtoolkit/index.html)
ver 0.2beta56 で動作確認済み。
### 使用ライブラリ
#### [Codeer.Friendly](https://github.com/Codeer-Software/Friendly)  - MIT License (ver 0.0.xで使用)
#### [NAudio](https://github.com/naudio/NAudio)  - Apache License 2.0 (ver 0.0.xで使用)
#### [AITalk Liblary](https://www.ai-j.jp) - Need a License (and Non-Commercial use)

### 謝辞
本ツールの作成に当たって以下の方々の公開してくださっているツールやコードを利用させていただきました<br>
記して感謝申し上げます<br>

#### Nkyoku様 ([voiceroid_daemon](https://github.com/Nkyoku/voiceroid_daemon) - MIT License)
#### oov様 ([aviutl_gcmzdrops](https://github.com/oov/aviutl_gcmzdrops) - MIT License)
#### [努力したＷｉｋｉ](https://hgotoh.jp/wiki/doku.php/start)様
