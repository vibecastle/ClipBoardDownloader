# Publicação Gratuita na Microsoft Store (MSIX)

Este guia mostra como criar sua conta gratuita de desenvolvedor individual na Microsoft Store, gerar o `.msix` oficial com a identidade da sua conta e publicar o **CBDownloader** sem precisar comprar certificado pago (a própria Microsoft assina o app quando publica na loja).

---

## 1. Criar conta gratuita de desenvolvedor individual

1. Acesse **<https://storedeveloper.microsoft.com>** e faça login com sua conta Microsoft.
2. Escolha conta **Individual** (gratuita globalmente, sem taxa de cartão de crédito).
3. Conclua a verificação de identidade enviando foto de um documento oficial + selfie pelo celular.

---

## 2. Reservar o nome e copiar os dados de identidade

1. No **Partner Center**, crie um novo aplicativo e reserve o nome **CBDownloader**.
2. Acesse **Gerenciamento de produtos → Identidade do produto** e copie estes 3 campos exatos:
   - **`Nome da identidade do pacote`** (ex.: `12345SeuNome.CBDownloader`)
   - **`Publicador`** (ex.: `CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX`)
   - **`Nome para exibição do publicador`** (seu nome exibido no Partner Center)

---

## 3. Gerar o `.msix` com a identidade oficial

### Opção A — Pelo GitHub Actions (recomendado)
1. No GitHub (`Settings → Secrets and variables → Actions → Variables`), crie 3 variáveis:
   - `MSIX_IDENTITY_NAME`
   - `MSIX_PUBLISHER`
   - `MSIX_PUBLISHER_DISPLAY_NAME`
2. Vá em **Actions → Build Microsoft Store MSIX → Run workflow**.
3. Baixe o artifact **`CBDownloader-MSIX-x64`** (`CBDownloader_<versão>_x64.msix`).

### Opção B — Localmente no PowerShell
```powershell
.\packaging\msix\build-msix.ps1 `
  -IdentityName "SEU_IDENTITY_NAME" `
  -Publisher "CN=SEU_PUBLISHER_GUID" `
  -PublisherDisplayName "SEU_PUBLISHER_DISPLAY_NAME"
```
> O pacote sai sem assinatura em `Output\CBDownloader_<versão>_x64.msix` — é exatamente assim que o Partner Center espera, porque a Microsoft aplica a assinatura oficial dela durante a certificação.

---

## 4. Textos prontos para enviar na Microsoft Store

### Política de Privacidade
```text
https://github.com/vibecastle/ClipBoardDownloader#privacy-policy
```

### Início da Descrição do App (obrigatório pela Política 10.2.4)
> Cole este bloco **logo no começo** da descrição na Microsoft Store:

```text
This application is a graphical user interface front-end for the bundled open-source tools yt-dlp, FFmpeg, and Deno. All media inspection, downloading, and conversion run locally on your device.

Notice: CBDownloader is intended solely for downloading and saving media that you own, have permission to download, or that is in the public domain or licensed under open terms. Users are solely responsible for complying with applicable copyright laws and the Terms of Service of any website they access.
```

### Observações para Certificação (`runFullTrust`)
> Cole no campo **Notes for certification** ao submeter:

```text
CBDownloader uses the restricted runFullTrust capability for three desktop functions:
1. Monitoring the Windows clipboard via a Win32 message-only / hidden window listener when the user copies supported media URLs.
2. Providing a Windows system tray (NotifyIcon) experience with a background toast popup.
3. Launching the locally bundled open-source CLI executables (yt-dlp.exe, ffmpeg.exe, and deno.exe included inside the package directory) when the user clicks Download.

No external binaries or application updates are downloaded or executed dynamically at runtime when packaged for the Microsoft Store; app startup is managed natively via Windows.ApplicationModel.StartupTask ("CBDownloaderStartupTask").
```
