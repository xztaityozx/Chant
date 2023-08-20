# Chant

画像に書かれた呪文を読み取り、コマンドにデコードして実行するやつ！

~ディレクトリ構成をミスったかもしれねえ~

# 使い方

```console
$ dotnet run ./path/to/image_file.png
```

* OCRエンジンにVisionApiを使いたい場合はGoogle Cloudへの認証を先に通しておく必要があります
* 内部でyukichantを動かすためにDockerを使用しています
    * Windows/MacではDocker　Desktopが必要かもしれません
