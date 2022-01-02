# Kiritanport (きりたんぽーと)
## VOICEROID2、VOICEVOX　統合フレーズエディタ
### 注意事項
未完成です<br>
マニュアルを含むドキュメントも未整備です<br>
<br>
・本ツールの動作には「VOICEROID2」「VOICEVOX」「AssistantSeika（及び対応している合成音声ソフト）」の内、最低でも1つ以上の外部ツールが必要です。<br>
・「VOICEROID2」は本ツール起動前及び起動中に「起動しない」でください。<br>
・「VOICEVOX」及び「AssistantSeika」は本ツール起動前に「起動しておいて」ください。<br>
・「VOICEROID2」のインストール場所をデフォルトから変更している場合、「settings.ini」ファイルを編集が必要です。<br>
・「フレーズ辞書」機能は「VOICEROID2」および「VOICEVOX」で利用可能です。<br>
・「単語辞書」機能の利用には「VOICEROID2」が必要です。<br>
（VOICEVOXで単語辞書を利用する場合は、使用する辞書をDefaultからVoiceroidへ変更してください）<br>
・「AssistantSeika」の設定で基本設定タブの「16bitのwavファイルを使用」及び「前後の無音部分削除」を選択してください。<br>
### 入手
https://github.com/Node-FBB/Kiritanport/releases
### マニュアル
https://github.com/Node-FBB/Kiritanport/wiki
### 更新履歴
0.3.0　テスト版公開<br>
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
#### VOICEROID2
VOICEROID音声の利用に必要（EXおよびEX+は不可、VOICEROID2にボイスライブラリをインポートすれば可）<br>
https://www.ah-soft.com<br>
#### VOICEVOX
VOICEVOX音声の利用に必要<br>
https://voicevox.hiroshiba.jp<br>
#### AssistantSeika
対応している多数の合成音声ソフトから音声を取得できます（アクセント等の変更はできません）<br>
https://hgotoh.jp/wiki/doku.php/start<br>
### 外部ツール（連携）
#### AviUtl
#### PSDToolKit
### 使用ライブラリ
#### Codeer.Friendly  - MIT License
https://github.com/Codeer-Software/Friendly
#### NAudio  - Apache License 2.0
https://github.com/naudio/NAudio
#### AITalk Liblary
https://www.ai-j.jp
### 謝辞
本ツールの作成に当たって以下の方々の公開してくださっているツールやコードを利用させていただきました<br>
記して感謝申し上げます<br>

#### Nkyoku様 (voiceroid_daemon - MIT License)
https://github.com/Nkyoku/voiceroid_daemon
#### oov様 (aviutl_gcmzdrops - MIT License)
https://github.com/oov/aviutl_gcmzdrops
#### 努力したＷｉｋｉ様
https://hgotoh.jp/wiki/doku.php/start
