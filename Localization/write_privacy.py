# -*- coding: utf-8 -*-
import json
from pathlib import Path

texts = {}

texts["es"] = """# Política de privacidad de SharpShot

Última actualización: 17 de septiembre de 2026

Editor: ZHU Industries LLC
Desarrollador: BMOandShiro

Esta es la política de privacidad de SharpShot, una aplicación de Windows para capturas de pantalla y grabación de pantalla. La copia vigente está en este repositorio para que siga disponible en:

https://github.com/BmoandShiro/SharpShot/blob/main/PRIVACY.md

## Qué es esta aplicación

SharpShot se ejecuta en tu PC. No requiere una cuenta. No hay inicio de sesión de SharpShot, biblioteca en la nube, red de publicidad ni servicio de analítica.

## Datos que se quedan en tu dispositivo

- Las capturas y las grabaciones se escriben solo en la carpeta de guardado que elijas. SharpShot no las sube.
- Los ajustes, incluidos los atajos y el tema, se guardan en local en `%AppData%\\SharpShot\\settings.json`.
- Pueden escribirse registros de depuración opcionales junto a la aplicación mientras está en ejecución. Esos registros no se nos envían.

Todo lo que haya en tu pantalla puede aparecer en una captura o grabación que tú elijas hacer. Esos archivos siguen siendo tuyos, en tu ordenador, hasta que tú mismo los compartas.

Si activas la captura del micrófono o del audio del sistema en una grabación, ese audio se escribe solo en el archivo de la grabación en este PC. SharpShot no sube el audio del micrófono ni el del sistema.

## Acceso a la red

Las copias de Steam y de Microsoft Store de SharpShot no consultan GitHub en busca de actualizaciones. Esas tiendas actualizan la aplicación. Esas copias tampoco descargan otras aplicaciones por ti.

Las copias distribuidas desde GitHub pueden contactar con GitHub si está activada la opción «Buscar actualizaciones automáticamente» o si eliges Comprobar ahora. Esa petición va a los servidores de GitHub (https://api.github.com) para ver si existe una versión pública más reciente, y puede descargar esa versión desde GitHub. La política de privacidad de GitHub se aplica a esa conexión. SharpShot no adjunta una cuenta ni un identificador de usuario de SharpShot a esa petición.

Capturar y grabar no requieren conexión a internet.

## Qué no hacemos

- No vendemos información personal.
- No mostramos anuncios ni analítica de terceros dentro de SharpShot.
- No operamos un servicio propio de informes de fallos ni de telemetría.
- No recogemos tu nombre, tu correo ni datos de pago. Las compras en Steam o en Microsoft Store las gestionan esas tiendas.

## Otros programas

FFmpeg va incluido para que la grabación funcione en este PC. Se ejecuta en local.

Puedes vincular un programa que ya tengas instalado y abrirlo desde SharpShot. Eso incluye una grabadora más avanzada. OBS Studio es un ejemplo. SharpShot no incluye esos programas, y las copias de Steam y de Microsoft Store no los descargan. Cada programa vinculado es una aplicación aparte, con sus propios términos y su propia política de privacidad.

## Menores

SharpShot es una utilidad de escritorio de uso general. No está dirigida a menores de 13 años, y no recogemos a sabiendas información personal de menores.

## Contacto y cambios

Las preguntas sobre esta política pueden enviarse como una incidencia de GitHub en https://github.com/BmoandShiro/SharpShot.

Si esta política cambia, el texto actualizado se publicará en este archivo. La fecha de «Última actualización» cambiará con él.
"""

texts["fr"] = """# Politique de confidentialité de SharpShot

Dernière mise à jour : 17 septembre 2026

Éditeur : ZHU Industries LLC
Développeur : BMOandShiro

Voici la politique de confidentialité de SharpShot, une application Windows de capture et d'enregistrement d'écran. La copie en vigueur est dans ce dépôt afin de rester disponible ici :

https://github.com/BmoandShiro/SharpShot/blob/main/PRIVACY.md

## Ce qu'est cette application

SharpShot s'exécute sur votre PC. Aucun compte n'est requis. Il n'y a pas de connexion SharpShot, de bibliothèque cloud, de réseau publicitaire ni de service d'analyse.

## Données qui restent sur votre appareil

- Les captures et les enregistrements sont écrits uniquement dans le dossier que vous choisissez. SharpShot ne les envoie pas.
- Les paramètres, y compris les raccourcis et le thème, sont stockés localement dans `%AppData%\\SharpShot\\settings.json`.
- Des journaux de débogage facultatifs peuvent être écrits à côté de l'application pendant qu'elle tourne. Ces journaux ne nous sont pas envoyés.

Tout ce qui est à l'écran peut apparaître dans une capture ou un enregistrement que vous choisissez de faire. Ces fichiers restent les vôtres, sur votre ordinateur, jusqu'à ce que vous les partagiez vous-même.

Si vous activez la capture du microphone ou de l'audio système pour un enregistrement, cet audio est écrit uniquement dans le fichier d'enregistrement sur ce PC. SharpShot n'envoie pas l'audio du microphone ni de l'audio système.

## Accès au réseau

Les copies Steam et Microsoft Store de SharpShot ne consultent pas GitHub pour les mises à jour. Ces boutiques mettent l'application à jour. Ces copies ne téléchargent pas non plus d'autres applications pour vous.

Les copies distribuées depuis GitHub peuvent contacter GitHub si « Vérifier automatiquement les mises à jour » est activé, ou si vous choisissez Vérifier maintenant. Cette requête va aux serveurs de GitHub (https://api.github.com) pour voir s'il existe une version publique plus récente, et peut télécharger cette version depuis GitHub. La politique de confidentialité de GitHub s'applique à cette connexion. SharpShot n'y joint ni compte ni identifiant utilisateur SharpShot.

La capture et l'enregistrement ne nécessitent pas de connexion Internet.

## Ce que nous ne faisons pas

- Nous ne vendons pas d'informations personnelles.
- Nous n'affichons pas de publicité ni d'analyse tierce dans SharpShot.
- Nous n'exploitons pas notre propre service de signalement de plantages ni de télémétrie.
- Nous ne collectons pas votre nom, votre adresse e-mail ni vos informations de paiement. Les achats sur Steam ou le Microsoft Store sont gérés par ces boutiques.

## Autres programmes

FFmpeg est inclus pour que l'enregistrement fonctionne sur ce PC. Il s'exécute localement.

Vous pouvez lier un programme déjà installé et l'ouvrir depuis SharpShot. Cela comprend un enregistreur plus avancé. OBS Studio en est un exemple. SharpShot n'inclut pas ces programmes, et les copies Steam et Microsoft Store ne les téléchargent pas. Chaque programme lié est une application distincte, avec ses propres conditions et sa propre politique de confidentialité.

## Enfants

SharpShot est un utilitaire de bureau général. Il ne s'adresse pas aux enfants de moins de 13 ans, et nous ne collectons pas sciemment d'informations personnelles auprès d'enfants.

## Contact et modifications

Les questions sur cette politique peuvent être envoyées comme un ticket GitHub sur https://github.com/BmoandShiro/SharpShot.

Si cette politique change, le texte mis à jour sera publié dans ce fichier. La date de « Dernière mise à jour » changera avec lui.
"""

texts["de"] = """# Datenschutzrichtlinie von SharpShot

Zuletzt aktualisiert: 17. September 2026

Herausgeber: ZHU Industries LLC
Entwickler: BMOandShiro

Dies ist die Datenschutzrichtlinie für SharpShot, eine Windows-App für Screenshots und Bildschirmaufnahmen. Die aktuelle Fassung liegt in diesem Repository, damit sie hier verfügbar bleibt:

https://github.com/BmoandShiro/SharpShot/blob/main/PRIVACY.md

## Was diese App ist

SharpShot läuft auf Ihrem PC. Ein Konto ist nicht nötig. Es gibt kein SharpShot-Login, keine Cloud-Bibliothek, kein Werbenetzwerk und keinen Analysedienst.

## Daten, die auf Ihrem Gerät bleiben

- Screenshots und Aufnahmen werden nur in den Speicherordner geschrieben, den Sie wählen. SharpShot lädt sie nicht hoch.
- Einstellungen, einschließlich Tastenkürzel und Design, werden lokal in `%AppData%\\SharpShot\\settings.json` gespeichert.
- Optionale Debug-Protokolle können neben der App geschrieben werden, während sie läuft. Diese Protokolle werden nicht an uns gesendet.

Alles auf Ihrem Bildschirm kann in einem Screenshot oder einer Aufnahme erscheinen, die Sie selbst erstellen. Diese Dateien bleiben Ihre, auf Ihrem Computer, bis Sie sie selbst weitergeben.

Wenn Sie Mikrofon- oder Systemaudio für eine Aufnahme aktivieren, wird dieses Audio nur in die Aufnahmedatei auf diesem PC geschrieben. SharpShot lädt weder Mikrofon- noch Systemaudio hoch.

## Netzwerkzugriff

Steam- und Microsoft-Store-Kopien von SharpShot fragen GitHub nicht nach Updates. Diese Stores aktualisieren die App. Diese Kopien laden auch keine anderen Anwendungen für Sie herunter.

Von GitHub verteilte Kopien können GitHub kontaktieren, wenn „Automatisch nach Updates suchen“ aktiv ist oder Sie Jetzt prüfen wählen. Die Anfrage geht an die Server von GitHub (https://api.github.com), damit die App sehen kann, ob es eine neuere öffentliche Version gibt, und diese Version von GitHub herunterladen kann. Die Datenschutzrichtlinie von GitHub gilt für diese Verbindung. SharpShot hängt weder ein Konto noch eine SharpShot-Benutzerkennung an diese Anfrage.

Aufnehmen und Aufzeichnen brauchen keine Internetverbindung.

## Was wir nicht tun

- Wir verkaufen keine personenbezogenen Daten.
- Wir schalten keine Werbung und keine Analyse Dritter in SharpShot.
- Wir betreiben keinen eigenen Absturzbericht- oder Telemetriedienst.
- Wir erheben nicht Ihren Namen, Ihre E-Mail-Adresse oder Zahlungsdaten. Käufe bei Steam oder im Microsoft Store wickeln diese Stores ab.

## Andere Programme

FFmpeg ist enthalten, damit die Aufnahme auf diesem PC funktioniert. Es läuft lokal.

Sie können ein bereits installiertes Programm verknüpfen und aus SharpShot öffnen. Dazu gehört ein umfangreicheres Aufnahmeprogramm. OBS Studio ist ein Beispiel. SharpShot enthält diese Programme nicht, und Steam- sowie Microsoft-Store-Kopien laden sie nicht herunter. Jedes verknüpfte Programm ist eine eigene App mit eigenen Bedingungen und einer eigenen Datenschutzrichtlinie.

## Kinder

SharpShot ist ein allgemeines Desktop-Programm. Es richtet sich nicht an Kinder unter 13 Jahren, und wir erheben wissentlich keine personenbezogenen Daten von Kindern.

## Kontakt und Änderungen

Fragen zu dieser Richtlinie können als GitHub-Issue unter https://github.com/BmoandShiro/SharpShot gesendet werden.

Wenn sich diese Richtlinie ändert, wird der aktualisierte Text in dieser Datei veröffentlicht. Das Datum „Zuletzt aktualisiert“ ändert sich mit.
"""

texts["pt"] = """# Política de privacidade do SharpShot

Última atualização: 17 de setembro de 2026

Editor: ZHU Industries LLC
Desenvolvedor: BMOandShiro

Esta é a política de privacidade do SharpShot, um aplicativo do Windows para capturas e gravação de tela. A cópia vigente está neste repositório para continuar disponível em:

https://github.com/BmoandShiro/SharpShot/blob/main/PRIVACY.md

## O que é este aplicativo

O SharpShot roda no seu PC. Não exige uma conta. Não há login do SharpShot, biblioteca na nuvem, rede de anúncios nem serviço de análise.

## Dados que ficam no seu dispositivo

- Capturas e gravações são gravadas apenas na pasta que você escolher. O SharpShot não as envia.
- As configurações, inclusive atalhos e tema, ficam no computador em `%AppData%\\SharpShot\\settings.json`.
- Registros de depuração opcionais podem ser escritos ao lado do aplicativo enquanto ele está em execução. Esses registros não são enviados a nós.

O que estiver na tela pode aparecer em uma captura ou gravação que você escolher fazer. Esses arquivos continuam seus, no seu computador, até que você mesmo os compartilhe.

Se você ativar a captura do microfone ou do áudio do sistema em uma gravação, esse áudio é gravado apenas no arquivo da gravação neste PC. O SharpShot não envia o áudio do microfone nem o áudio do sistema.

## Acesso à rede

As cópias da Steam e da Microsoft Store do SharpShot não consultam o GitHub em busca de atualizações. Essas lojas atualizam o aplicativo. Essas cópias também não baixam outros aplicativos para você.

As cópias distribuídas pelo GitHub podem contactar o GitHub se «Verificar atualizações automaticamente» estiver ativado, ou se você escolher Verificar agora. Esse pedido vai aos servidores do GitHub (https://api.github.com) para ver se existe uma versão pública mais nova, e pode baixar essa versão do GitHub. A política de privacidade do GitHub se aplica a essa conexão. O SharpShot não anexa uma conta nem um identificador de usuário do SharpShot a esse pedido.

Capturar e gravar não exigem conexão com a internet.

## O que não fazemos

- Não vendemos informações pessoais.
- Não exibimos anúncios nem análise de terceiros dentro do SharpShot.
- Não operamos um serviço próprio de relatos de falha nem de telemetria.
- Não coletamos seu nome, e-mail nem dados de pagamento. Compras na Steam ou na Microsoft Store são tratadas por essas lojas.

## Outros programas

O FFmpeg está incluído para a gravação funcionar neste PC. Ele roda localmente.

Você pode vincular um programa que já instalou e abri-lo pelo SharpShot. Isso inclui um gravador mais avançado. O OBS Studio é um exemplo. O SharpShot não inclui esses programas, e as cópias da Steam e da Microsoft Store não os baixam. Cada programa vinculado é um aplicativo separado, com os próprios termos e a própria política de privacidade.

## Crianças

O SharpShot é um utilitário de área de trabalho de uso geral. Não é dirigido a crianças menores de 13 anos, e não coletamos intencionalmente informações pessoais de crianças.

## Contato e alterações

Perguntas sobre esta política podem ser enviadas como um issue no GitHub em https://github.com/BmoandShiro/SharpShot.

Se esta política mudar, o texto atualizado será publicado neste arquivo. A data de «Última atualização» mudará junto.
"""

texts["it"] = """# Informativa privacy di SharpShot

Ultimo aggiornamento: 17 settembre 2026

Editore: ZHU Industries LLC
Sviluppatore: BMOandShiro

Questa è l'informativa privacy di SharpShot, un'app Windows per screenshot e registrazione dello schermo. La copia vigente è in questo repository così resta disponibile qui:

https://github.com/BmoandShiro/SharpShot/blob/main/PRIVACY.md

## Che cos'è questa app

SharpShot gira sul tuo PC. Non serve un account. Non c'è un accesso SharpShot, una libreria cloud, una rete pubblicitaria o un servizio di analisi.

## Dati che restano sul dispositivo

- Screenshot e registrazioni vengono scritti solo nella cartella che scegli. SharpShot non li carica.
- Le impostazioni, incluse scorciatoie e tema, sono salvate in locale in `%AppData%\\SharpShot\\settings.json`.
- Durante l'esecuzione possono essere scritti registri di debug facoltativi accanto all'app. Quei registri non ci vengono inviati.

Tutto ciò che è sullo schermo può comparire in uno screenshot o in una registrazione che scegli di fare. Quei file restano tuoi, sul tuo computer, finché non li condividi tu.

Se attivi la cattura del microfono o dell'audio di sistema per una registrazione, quell'audio viene scritto solo nel file di registrazione su questo PC. SharpShot non carica l'audio del microfono né l'audio di sistema.

## Accesso alla rete

Le copie Steam e Microsoft Store di SharpShot non controllano GitHub per gli aggiornamenti. Quei negozi aggiornano l'app. Quelle copie non scaricano altre applicazioni per te.

Le copie distribuite da GitHub possono contattare GitHub se è attiva l'opzione «Controlla automaticamente gli aggiornamenti» o se scegli Controlla ora. La richiesta va ai server di GitHub (https://api.github.com) per vedere se esiste una versione pubblica più recente, e può scaricare quella versione da GitHub. L'informativa privacy di GitHub si applica a quella connessione. SharpShot non allega un account né un id utente SharpShot a quella richiesta.

Acquisire e registrare non richiede una connessione a internet.

## Cosa non facciamo

- Non vendiamo informazioni personali.
- Non mostriamo pubblicità né analisi di terze parti dentro SharpShot.
- Non gestiamo un nostro servizio di segnalazione arresti o di telemetria.
- Non raccogliamo nome, email o dati di pagamento. Gli acquisti su Steam o sul Microsoft Store sono gestiti da quei negozi.

## Altri programmi

FFmpeg è incluso perché la registrazione funzioni su questo PC. Gira in locale.

Puoi collegare un programma già installato e aprirlo da SharpShot. Questo include un registratore più avanzato. OBS Studio è un esempio. SharpShot non include quei programmi, e le copie Steam e Microsoft Store non li scaricano. Ogni programma collegato è un'app separata, con i propri termini e la propria informativa privacy.

## Minori

SharpShot è un'utilità desktop di uso generale. Non è rivolta a minori di 13 anni e non raccogliamo consapevolmente informazioni personali da minori.

## Contatto e modifiche

Le domande su questa informativa possono essere inviate come issue su GitHub a https://github.com/BmoandShiro/SharpShot.

Se questa informativa cambia, il testo aggiornato sarà pubblicato in questo file. La data di «Ultimo aggiornamento» cambierà con esso.
"""

texts["ru"] = """# Политика конфиденциальности SharpShot

Последнее обновление: 17 сентября 2026 г.

Издатель: ZHU Industries LLC
Разработчик: BMOandShiro

Это политика конфиденциальности SharpShot — приложения Windows для снимков и записи экрана. Действующая копия хранится в этом репозитории, чтобы оставаться доступной по адресу:

https://github.com/BmoandShiro/SharpShot/blob/main/PRIVACY.md

## Что это за приложение

SharpShot работает на вашем компьютере. Учётная запись не нужна. Нет входа SharpShot, облачной библиотеки, рекламной сети или службы аналитики.

## Данные, которые остаются на устройстве

- Снимки и записи сохраняются только в выбранную вами папку. SharpShot их не отправляет.
- Параметры, включая горячие клавиши и тему, хранятся локально в `%AppData%\\SharpShot\\settings.json`.
- Пока приложение работает, рядом с ним могут записываться необязательные журналы отладки. Они нам не отправляются.

Всё, что есть на экране, может попасть в снимок или запись, которые вы сами создаёте. Эти файлы остаются вашими на вашем компьютере, пока вы сами ими не поделитесь.

Если вы включите запись с микрофона или системного звука, этот звук записывается только в файл записи на этом ПК. SharpShot не загружает звук с микрофона и системный звук.

## Доступ в сеть

Копии Steam и Microsoft Store не проверяют GitHub на наличие обновлений. Эти магазины обновляют приложение сами. Эти копии также не скачивают для вас другие программы.

Копии с GitHub могут обращаться к GitHub, если включено «Автоматически проверять обновления» или если вы нажмёте «Проверить». Запрос идёт на серверы GitHub (https://api.github.com), чтобы узнать, есть ли более новый публичный выпуск, и может скачать этот выпуск с GitHub. К этому соединению применяется политика конфиденциальности GitHub. SharpShot не прикладывает к запросу учётную запись или идентификатор пользователя SharpShot.

Снимки и запись не требуют подключения к интернету.

## Чего мы не делаем

- Мы не продаём персональные данные.
- Мы не показываем рекламу и стороннюю аналитику внутри SharpShot.
- Мы не ведём собственную службу отчётов о сбоях или телеметрии.
- Мы не собираем ваше имя, адрес электронной почты или платёжные данные. Покупки в Steam и Microsoft Store обрабатывают эти магазины.

## Другие программы

FFmpeg включён, чтобы запись работала на этом компьютере. Он выполняется локально.

Вы можете связать уже установленную программу и открывать её из SharpShot. В том числе более сложную программу записи. OBS Studio — один из примеров. SharpShot не включает эти программы, а копии Steam и Microsoft Store их не скачивают. Каждая связанная программа — отдельное приложение со своими условиями и политикой конфиденциальности.

## Дети

SharpShot — обычная настольная утилита. Она не предназначена для детей младше 13 лет, и мы сознательно не собираем персональные данные детей.

## Контакты и изменения

Вопросы об этой политике можно отправить как issue на GitHub: https://github.com/BmoandShiro/SharpShot.

Если политика изменится, обновлённый текст будет опубликован в этом файле. Дата «Последнее обновление» изменится вместе с ним.
"""

texts["ja"] = """# SharpShot プライバシー ポリシー

最終更新: 2026年9月17日

発行者: ZHU Industries LLC
開発者: BMOandShiro

これは、Windows 用のスクリーンショットおよび画面録画アプリである SharpShot のプライバシー ポリシーです。現行版はこのリポジトリにあり、次の場所で参照できます。

https://github.com/BmoandShiro/SharpShot/blob/main/PRIVACY.md

## このアプリについて

SharpShot はお使いの PC 上で動作します。アカウントは不要です。SharpShot のログイン、クラウド ライブラリ、広告ネットワーク、分析サービスはありません。

## デバイスに残るデータ

- スクリーンショットと録画は、選んだ保存フォルダーにだけ書き込まれます。SharpShot はそれらをアップロードしません。
- ホットキーやテーマを含む設定は、`%AppData%\\SharpShot\\settings.json` にローカル保存されます。
- 実行中、アプリのそばに任意のデバッグ ログが書かれることがあります。そのログは当社には送信されません。

画面にあるものは、あなたが選んで撮ったスクリーンショットや録画に写ることがあります。それらのファイルは、あなたが自分で共有するまで、あなたのコンピューター上のあなたのものです。

マイクまたはシステム音声のキャプチャを録画で有効にした場合、その音声はこの PC 上の録画ファイルにだけ書き込まれます。SharpShot はマイク音声もシステム音声もアップロードしません。

## ネットワークアクセス

Steam 版と Microsoft Store 版の SharpShot は、更新のために GitHub を確認しません。更新は各ストアが行います。これらの版は、他のアプリケーションを代わりにダウンロードすることもありません。

GitHub から配布される版は、「更新を自動で確認」がオンのとき、または「今すぐ確認」を選んだときに GitHub へ接続することがあります。その要求は GitHub のサーバー (https://api.github.com) に送られ、新しい公開版があるかを確認し、その版を GitHub からダウンロードすることがあります。その接続には GitHub のプライバシー ポリシーが適用されます。SharpShot はその要求にアカウントや SharpShot のユーザー ID を付けません。

キャプチャと録画にインターネット接続は不要です。

## 当社が行わないこと

- 個人情報を販売しません。
- SharpShot 内で広告や第三者分析を実行しません。
- 独自のクラッシュ報告やテレメトリ サービスを運営しません。
- 氏名、メールアドレス、支払い情報は収集しません。Steam または Microsoft Store での購入は、それらのストアが処理します。

## 他のプログラム

録画がこの PC で動くよう、FFmpeg が含まれています。ローカルで動作します。

すでにインストールしたプログラムをリンクし、SharpShot から開くことができます。より高機能な録画アプリも含まれます。OBS Studio はその一例です。SharpShot はそれらのプログラムを含まず、Steam 版と Microsoft Store 版もそれらをダウンロードしません。リンクした各プログラムは別のアプリであり、独自の規約とプライバシー ポリシーがあります。

## 子ども

SharpShot は一般向けのデスクトップ ユーティリティです。13 歳未満の子どもを対象としておらず、子どもの個人情報を故意に収集しません。

## 連絡と変更

このポリシーについての質問は、https://github.com/BmoandShiro/SharpShot の GitHub issue として送れます。

このポリシーが変わった場合、更新された文面はこのファイルで公開されます。上の「最終更新」の日付も一緒に変わります。
"""

texts["ko"] = """# SharpShot 개인정보 처리방침

최종 업데이트: 2026년 9월 17일

발행자: ZHU Industries LLC
개발자: BMOandShiro

이것은 Windows용 스크린샷 및 화면 녹화 앱 SharpShot의 개인정보 처리방침입니다. 현재 문서는 이 저장소에 있어 다음 주소에서 볼 수 있습니다.

https://github.com/BmoandShiro/SharpShot/blob/main/PRIVACY.md

## 이 앱은 무엇인가

SharpShot은 사용자 PC에서 실행됩니다. 계정이 필요하지 않습니다. SharpShot 로그인, 클라우드 보관함, 광고 네트워크, 분석 서비스는 없습니다.

## 기기에 남는 데이터

- 스크린샷과 녹화는 사용자가 고른 저장 폴더에만 기록됩니다. SharpShot은 이를 올리지 않습니다.
- 단축키와 테마를 포함한 설정은 `%AppData%\\SharpShot\\settings.json`에 로컬로 저장됩니다.
- 실행 중 앱 옆에 선택적 디버그 로그가 기록될 수 있습니다. 그 로그는 저희에게 전송되지 않습니다.

화면에 있는 것은 사용자가 직접 찍기로 한 스크린샷이나 녹화에 나타날 수 있습니다. 그 파일은 사용자가 직접 공유하기 전까지 사용자 컴퓨터에 있는 사용자 것입니다.

녹화에서 마이크 또는 시스템 오디오 캡처를 켜면, 그 오디오는 이 PC의 녹화 파일에만 기록됩니다. SharpShot은 마이크나 시스템 오디오를 업로드하지 않습니다.

## 네트워크 접근

Steam 및 Microsoft Store 판 SharpShot은 업데이트를 위해 GitHub를 확인하지 않습니다. 해당 스토어가 앱을 업데이트합니다. 이 판은 다른 애플리케이션을 대신 내려받지도 않습니다.

GitHub에서 배포된 판은 «업데이트 자동 확인»이 켜져 있거나 지금 확인을 선택하면 GitHub에 연결할 수 있습니다. 그 요청은 GitHub 서버(https://api.github.com)로 가서 더 새로운 공개 버전이 있는지 확인하고, 그 버전을 GitHub에서 내려받을 수 있습니다. 그 연결에는 GitHub 개인정보 처리방침이 적용됩니다. SharpShot은 그 요청에 계정이나 SharpShot 사용자 ID를 붙이지 않습니다.

캡처와 녹화에는 인터넷 연결이 필요하지 않습니다.

## 하지 않는 일

- 개인정보를 판매하지 않습니다.
- SharpShot 안에서 광고나 제3자 분석을 실행하지 않습니다.
- 자체 충돌 보고나 원격 측정 서비스를 운영하지 않습니다.
- 이름, 이메일, 결제 정보를 수집하지 않습니다. Steam 또는 Microsoft Store 구매는 해당 스토어가 처리합니다.

## 다른 프로그램

이 PC에서 녹화가 되도록 FFmpeg가 포함되어 있습니다. 로컬에서 실행됩니다.

이미 설치한 프로그램을 연결해 SharpShot에서 열 수 있습니다. 더 복잡한 녹화 프로그램도 해당됩니다. OBS Studio는 한 예입니다. SharpShot은 그 프로그램을 포함하지 않으며, Steam 및 Microsoft Store 판도 내려받지 않습니다. 연결한 각 프로그램은 자체 약관과 개인정보 처리방침이 있는 별도 앱입니다.

## 아동

SharpShot은 일반적인 데스크톱 유틸리티입니다. 13세 미만 아동을 대상으로 하지 않으며, 아동의 개인정보를 고의로 수집하지 않습니다.

## 문의와 변경

이 방침에 대한 질문은 https://github.com/BmoandShiro/SharpShot 의 GitHub 이슈로 보낼 수 있습니다.

방침이 바뀌면 업데이트된 글이 이 파일에 게시됩니다. 위의 «최종 업데이트» 날짜도 함께 바뀝니다.
"""

texts["zh-Hans"] = """# SharpShot 隐私政策

最后更新：2026年9月17日

发布者：ZHU Industries LLC
开发者：BMOandShiro

这是 SharpShot 的隐私政策。SharpShot 是一款 Windows 截图和屏幕录制应用。现行文本放在此仓库中，以便在此查阅：

https://github.com/BmoandShiro/SharpShot/blob/main/PRIVACY.md

## 本应用是什么

SharpShot 在你的电脑上运行。不需要账号。没有 SharpShot 登录、云端图库、广告网络或分析服务。

## 留在你设备上的数据

- 截图和录制只写入你选择的保存文件夹。SharpShot 不会上传它们。
- 设置（包括热键和主题）保存在本地的 `%AppData%\\SharpShot\\settings.json`。
- 应用运行时，可能会在程序旁边写入可选的调试日志。这些日志不会发送给我们。

屏幕上的内容可能会出现在你选择拍摄的截图或录制中。在你自己分享之前，这些文件仍属于你，并留在你的电脑上。

如果在录制中启用麦克风或系统音频捕获，该音频只会写入本机上的录制文件。SharpShot 不会上传麦克风或系统音频。

## 网络访问

Steam 和 Microsoft Store 版本的 SharpShot 不会向 GitHub 检查更新。这些商店负责更新应用。这些版本也不会替你下载其他应用程序。

从 GitHub 分发的版本，如果启用了“自动检查更新”，或你选择“立即检查”，可能会联系 GitHub。该请求发往 GitHub 的服务器（https://api.github.com），以便查看是否有更新的公开发布，并可能从 GitHub 下载该发布。该连接适用 GitHub 的隐私政策。SharpShot 不会在该请求中附带账号或 SharpShot 用户 ID。

截图和录制不需要互联网连接。

## 我们不做的事

- 我们不出售个人信息。
- 我们不在 SharpShot 内投放广告或第三方分析。
- 我们不运营自己的崩溃报告或遥测服务。
- 我们不收集你的姓名、电子邮件或付款信息。在 Steam 或 Microsoft Store 上的购买由这些商店处理。

## 其他程序

为使录制能在这台电脑上工作，应用包含 FFmpeg。它在本地运行。

你可以链接已经安装的程序，并从 SharpShot 打开它。这包括更复杂的录制程序。OBS Studio 是一个例子。SharpShot 不包含这些程序，Steam 和 Microsoft Store 版本也不会下载它们。每个已链接的程序都是独立应用，有自己的条款和隐私政策。

## 儿童

SharpShot 是通用桌面工具。它不面向 13 岁以下儿童，我们也不会故意收集儿童的个人信息。

## 联系和变更

关于本政策的问题可以作为 GitHub issue 发送到 https://github.com/BmoandShiro/SharpShot。

如果本政策变更，更新后的文本会发布在此文件中。上方的“最后更新”日期会一并更改。
"""

texts["nl"] = """# Privacybeleid van SharpShot

Laatst bijgewerkt: 17 september 2026

Uitgever: ZHU Industries LLC
Ontwikkelaar: BMOandShiro

Dit is het privacybeleid van SharpShot, een Windows-app voor screenshots en schermopnames. De actuele tekst staat in deze repository zodat die beschikbaar blijft op:

https://github.com/BmoandShiro/SharpShot/blob/main/PRIVACY.md

## Wat deze app is

SharpShot draait op je pc. Er is geen account nodig. Er is geen SharpShot-login, cloudbibliotheek, advertentienetwerk of analyseservice.

## Gegevens die op je apparaat blijven

- Screenshots en opnames worden alleen naar de opslagmap geschreven die jij kiest. SharpShot uploadt ze niet.
- Instellingen, inclusief sneltoetsen en thema, worden lokaal opgeslagen in `%AppData%\\SharpShot\\settings.json`.
- Optionele foutopsporingslogboeken kunnen naast de app worden geschreven terwijl die draait. Die logboeken worden niet naar ons gestuurd.

Alles wat op je scherm staat, kan in een screenshot of opname terechtkomen die jij maakt. Die bestanden blijven van jou, op je computer, tot je ze zelf deelt.

Als je microfoon- of systeemaudio-opname inschakelt, wordt die audio alleen in het opnamebestand op deze pc geschreven. SharpShot uploadt geen microfoon- of systeemaudio.

## Netwerktoegang

Steam- en Microsoft Store-versies van SharpShot controleren GitHub niet op updates. Die winkels werken de app bij. Die versies downloaden ook geen andere toepassingen voor je.

Versies die via GitHub worden verspreid, kunnen GitHub contacteren als «Automatisch controleren op updates» is ingeschakeld, of als je Nu controleren kiest. Dat verzoek gaat naar de servers van GitHub (https://api.github.com) om te zien of er een nieuwere openbare release is, en kan die release van GitHub downloaden. Het privacybeleid van GitHub geldt voor die verbinding. SharpShot voegt geen account of SharpShot-gebruikers-id toe aan dat verzoek.

Vastleggen en opnemen vereisen geen internetverbinding.

## Wat we niet doen

- We verkopen geen persoonlijke gegevens.
- We tonen geen advertenties of analyses van derden in SharpShot.
- We exploiteren geen eigen crashrapportage- of telemetrieservice.
- We verzamelen je naam, e-mailadres of betalingsgegevens niet. Aankopen in Steam of de Microsoft Store worden door die winkels afgehandeld.

## Andere programma's

FFmpeg is meegeleverd zodat opnemen op deze pc werkt. Het draait lokaal.

Je kunt een programma dat je al hebt geïnstalleerd koppelen en vanuit SharpShot openen. Dat kan een geavanceerdere recorder zijn. OBS Studio is een voorbeeld. SharpShot bevat die programma's niet, en Steam- en Microsoft Store-versies downloaden ze niet. Elk gekoppeld programma is een aparte app met eigen voorwaarden en privacybeleid.

## Kinderen

SharpShot is een algemene desktoputility. Het is niet gericht op kinderen onder de 13, en we verzamelen niet bewust persoonlijke gegevens van kinderen.

## Contact en wijzigingen

Vragen over dit beleid kun je als GitHub-issue sturen op https://github.com/BmoandShiro/SharpShot.

Als dit beleid wijzigt, wordt de bijgewerkte tekst in dit bestand gepubliceerd. De datum «Laatst bijgewerkt» wijzigt mee.
"""

out = Path(__file__).with_name("privacy.json")
out.write_text(json.dumps(texts, ensure_ascii=False, indent=2), encoding="utf-8")
print("privacy", len(texts), "->", out)
