using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using SharpShot.Models;

namespace SharpShot.Services
{
    public sealed class OcrLanguageChoice
    {
        public OcrLanguageChoice(string code, string label)
        {
            Code = code;
            Label = label;
        }

        public string Code { get; }
        public string Label { get; }
    }

    public sealed class LanguageOption
    {
        public required string AppId { get; init; }
        public required string OcrCode { get; init; }
        public required string NativeName { get; init; }
    }

    /// <summary>
    /// UI strings for the app language picker. OCR language is separate and uses Tesseract traineddata.
    /// </summary>
    public static class LocalizationService
    {
        public static readonly LanguageOption[] Languages =
        {
            new() { AppId = "en", OcrCode = "eng", NativeName = "English" },
            new() { AppId = "es", OcrCode = "spa", NativeName = "Español" },
            new() { AppId = "fr", OcrCode = "fra", NativeName = "Français" },
            new() { AppId = "de", OcrCode = "deu", NativeName = "Deutsch" },
            new() { AppId = "pt", OcrCode = "por", NativeName = "Português" },
            new() { AppId = "it", OcrCode = "ita", NativeName = "Italiano" },
            new() { AppId = "ru", OcrCode = "rus", NativeName = "Русский" },
            new() { AppId = "ja", OcrCode = "jpn", NativeName = "日本語" },
            new() { AppId = "ko", OcrCode = "kor", NativeName = "한국어" },
            new() { AppId = "zh-Hans", OcrCode = "chi_sim", NativeName = "中文（简体）" },
        };

        public static event Action? LanguageChanged;

        public static string CurrentAppLanguage { get; private set; } = "en";

        public static void ApplySavedLanguage()
        {
            try
            {
                SetLanguage(App.SettingsService?.CurrentSettings?.AppLanguage);
            }
            catch
            {
                SetLanguage("en");
            }
        }

        public static void SetLanguage(string? appId)
        {
            CurrentAppLanguage = string.IsNullOrWhiteSpace(appId) ? "en" : appId;
            if (!_tables.ContainsKey(CurrentAppLanguage))
                CurrentAppLanguage = "en";
            LanguageChanged?.Invoke();
        }

        public static string Get(string key)
        {
            if (_tables.TryGetValue(CurrentAppLanguage, out var table) && table.TryGetValue(key, out var value))
                return value;
            if (_tables["en"].TryGetValue(key, out var english))
                return english;
            return key;
        }

        public static OcrLanguageChoice[] GetOcrChoices()
        {
            return new[]
            {
                new OcrLanguageChoice("auto", AutoOcrLabel()),
            }.Concat(Languages.Select(lang => new OcrLanguageChoice(lang.OcrCode, lang.NativeName))).ToArray();
        }

        private static string AutoOcrLabel()
        {
            return CurrentAppLanguage switch
            {
                "es" => "Automático (idiomas latinos)",
                "fr" => "Automatique (langues latines)",
                "de" => "Automatisch (lateinische Sprachen)",
                "pt" => "Automático (idiomas latinos)",
                "it" => "Automatico (lingue latine)",
                "ru" => "Авто (латиница)",
                "ja" => "自動（ラテン文字）",
                "ko" => "자동 (라틴 문자)",
                "zh-Hans" => "自动（拉丁字母语言）",
                _ => "Auto (Latin languages)"
            };
        }

        public static void ApplyMainWindow(Window window)
        {
            SetTip(window, "RegionButton", "tip.region");
            SetTip(window, "ScreenshotButton", "tip.fullscreen");
            SetTip(window, "OcrRegionButton", "tip.ocr");
            SetTip(window, "SmartRegionToggleButton", "tip.smart");
            SetTip(window, "RecordingButton", "tip.record");
            SetTip(window, "SettingsButton", "tip.settings");
            SetTip(window, "MinimizeButton", "tip.minimize");
            SetTip(window, "CloseButton", "tip.close");
            SetTip(window, "CopyButton", "tip.copy");
            SetTip(window, "SaveButton", "tip.save");
            SetTip(window, "CancelButton", "tip.cancel");
            SetTip(window, "RegionRecordButton", "tip.recordRegion");
            SetTip(window, "FullScreenRecordButton", "tip.recordFullscreen");
            SetTip(window, "OBSRecordButton", "tip.obs");
            SetTip(window, "StopRecordButton", "tip.stop");
        }

        private static void SetTip(Window window, string name, string key)
        {
            if (window.FindName(name) is FrameworkElement element)
                element.ToolTip = Get(key);
        }

        private static readonly Dictionary<string, Dictionary<string, string>> _tables = new()
        {
            ["en"] = T(
                "General", "Capture", "Recording", "Dashboard", "Appearance", "Hotkeys", "Updates",
                "Settings", "Privacy", "Language", "App language",
                "Controls SharpShot's menus and tooltips. OCR language is separate.",
                "OCR language",
                "Auto reads English, Spanish, French, German, Portuguese, and Italian together. Pick Japanese, Chinese, Korean, or Russian when the screen uses that script.",
                "Storage", "Save Path:", "Browse",
                "Select Region", "Full Screen Screenshot", "OCR Region Capture",
                "Toggle Smart Regions — highlights text on the active window; click a highlight to copy it",
                "Toggle Recording", "Settings", "Minimize SharpShot", "Close SharpShot",
                "Copy to Clipboard", "Save File", "Cancel",
                "Record Selected Region (FFmpeg)", "Record Full Screen (FFmpeg)", "Launch OBS Studio", "Stop Recording",
                "OCR Quick Capture",
                "Text recognition needs language data next to SharpShot. English is included. Choose another OCR language in Settings if that file is installed.",
                "Open the application folder now?"),
            ["es"] = T(
                "General", "Captura", "Grabación", "Panel", "Apariencia", "Atajos", "Actualizaciones",
                "Ajustes", "Privacidad", "Idioma", "Idioma de la app",
                "Cambia los menús y la ayuda de SharpShot. El idioma del OCR es aparte.",
                "Idioma de OCR",
                "Automático lee inglés, español, francés, alemán, portugués e italiano juntos. Elige japonés, chino, coreano o ruso si la pantalla usa ese alfabeto.",
                "Almacenamiento", "Carpeta:", "Examinar",
                "Seleccionar región", "Captura de pantalla completa", "Captura OCR de región",
                "Regiones inteligentes: resalta el texto de la ventana activa; clic para copiarlo",
                "Grabar", "Ajustes", "Minimizar", "Cerrar",
                "Copiar", "Guardar", "Cancelar",
                "Grabar región (FFmpeg)", "Grabar pantalla completa (FFmpeg)", "Abrir OBS Studio", "Detener grabación",
                "Captura OCR",
                "El reconocimiento de texto necesita los datos de idioma junto a SharpShot. Inglés está incluido.",
                "¿Abrir la carpeta de la aplicación?"),
            ["fr"] = T(
                "Général", "Capture", "Enregistrement", "Tableau", "Apparence", "Raccourcis", "Mises à jour",
                "Paramètres", "Confidentialité", "Langue", "Langue de l'application",
                "Change les menus et infobulles de SharpShot. La langue OCR est séparée.",
                "Langue OCR",
                "Automatique lit l'anglais, l'espagnol, le français, l'allemand, le portugais et l'italien ensemble. Choisissez japonais, chinois, coréen ou russe si l'écran utilise cet alphabet.",
                "Stockage", "Dossier :", "Parcourir",
                "Sélectionner une zone", "Capture plein écran", "Capture OCR d'une zone",
                "Zones intelligentes : surligne le texte de la fenêtre active ; cliquez pour copier",
                "Enregistrer", "Paramètres", "Réduire", "Fermer",
                "Copier", "Enregistrer le fichier", "Annuler",
                "Enregistrer la zone (FFmpeg)", "Enregistrer tout l'écran (FFmpeg)", "Lancer OBS Studio", "Arrêter",
                "Capture OCR",
                "La reconnaissance de texte a besoin des données de langue à côté de SharpShot. L'anglais est inclus.",
                "Ouvrir le dossier de l'application ?"),
            ["de"] = T(
                "Allgemein", "Aufnahme", "Bildschirmvideo", "Übersicht", "Darstellung", "Tasten", "Updates",
                "Einstellungen", "Datenschutz", "Sprache", "App-Sprache",
                "Ändert Menüs und Hinweise. Die OCR-Sprache ist getrennt.",
                "OCR-Sprache",
                "Automatisch liest Englisch, Spanisch, Französisch, Deutsch, Portugiesisch und Italienisch zusammen. Japanisch, Chinesisch, Koreanisch oder Russisch wählen, wenn der Bildschirm diese Schrift nutzt.",
                "Speicher", "Speicherort:", "Durchsuchen",
                "Bereich wählen", "Vollbild-Screenshot", "OCR-Bereich",
                "Intelligente Bereiche: markiert Text im aktiven Fenster; Klick kopiert ihn",
                "Aufnahme", "Einstellungen", "Minimieren", "Schließen",
                "Kopieren", "Speichern", "Abbrechen",
                "Bereich aufnehmen (FFmpeg)", "Vollbild aufnehmen (FFmpeg)", "OBS Studio starten", "Aufnahme stoppen",
                "OCR-Erfassung",
                "Texterkennung braucht Sprachdaten neben SharpShot. Englisch ist enthalten.",
                "Anwendungsordner jetzt öffnen?"),
            ["pt"] = T(
                "Geral", "Captura", "Gravação", "Painel", "Aparência", "Atalhos", "Atualizações",
                "Configurações", "Privacidade", "Idioma", "Idioma do aplicativo",
                "Altera menus e dicas. O idioma do OCR é separado.",
                "Idioma do OCR",
                "Automático lê inglês, espanhol, francês, alemão, português e italiano juntos. Escolha japonês, chinês, coreano ou russo se a tela usar esse alfabeto.",
                "Armazenamento", "Pasta:", "Procurar",
                "Selecionar região", "Captura de tela inteira", "Captura OCR da região",
                "Regiões inteligentes: destaca o texto da janela ativa; clique para copiar",
                "Gravar", "Configurações", "Minimizar", "Fechar",
                "Copiar", "Salvar", "Cancelar",
                "Gravar região (FFmpeg)", "Gravar tela inteira (FFmpeg)", "Abrir OBS Studio", "Parar gravação",
                "Captura OCR",
                "O reconhecimento de texto precisa dos dados de idioma ao lado do SharpShot. Inglês está incluído.",
                "Abrir a pasta do aplicativo agora?"),
            ["it"] = T(
                "Generale", "Cattura", "Registrazione", "Pannello", "Aspetto", "Tasti", "Aggiornamenti",
                "Impostazioni", "Privacy", "Lingua", "Lingua dell'app",
                "Cambia menu e suggerimenti. La lingua OCR è separata.",
                "Lingua OCR",
                "Automatico legge inglese, spagnolo, francese, tedesco, portoghese e italiano insieme. Scegli giapponese, cinese, coreano o russo se lo schermo usa quell'alfabeto.",
                "Archiviazione", "Cartella:", "Sfoglia",
                "Seleziona area", "Screenshot a schermo intero", "Cattura OCR dell'area",
                "Aree intelligenti: evidenzia il testo della finestra attiva; clic per copiare",
                "Registra", "Impostazioni", "Riduci", "Chiudi",
                "Copia", "Salva", "Annulla",
                "Registra area (FFmpeg)", "Registra schermo intero (FFmpeg)", "Apri OBS Studio", "Ferma registrazione",
                "Cattura OCR",
                "Il riconoscimento del testo richiede i dati lingua accanto a SharpShot. L'inglese è incluso.",
                "Aprire la cartella dell'applicazione?"),
            ["ru"] = T(
                "Общие", "Захват", "Запись", "Панель", "Оформление", "Клавиши", "Обновления",
                "Настройки", "Конфиденциальность", "Язык", "Язык приложения",
                "Меняет меню и подсказки. Язык OCR задаётся отдельно.",
                "Язык OCR",
                "Авто читает английский, испанский, французский, немецкий, португальский и итальянский вместе. Выберите японский, китайский, корейский или русский, если экран на этой письменности.",
                "Сохранение", "Папка:", "Обзор",
                "Выбрать область", "Снимок всего экрана", "OCR области",
                "Умные области: подсвечивает текст активного окна; щёлкните, чтобы скопировать",
                "Запись", "Настройки", "Свернуть", "Закрыть",
                "Копировать", "Сохранить", "Отмена",
                "Запись области (FFmpeg)", "Запись всего экрана (FFmpeg)", "Запустить OBS Studio", "Остановить запись",
                "OCR-захват",
                "Распознаванию текста нужны языковые данные рядом с SharpShot. Английский уже включён.",
                "Открыть папку приложения?"),
            ["ja"] = T(
                "一般", "キャプチャ", "録画", "ダッシュボード", "外観", "ホットキー", "更新",
                "設定", "プライバシー", "言語", "アプリの言語",
                "メニューとヒントの言語です。画面の文字認識は別の設定です。",
                "OCRの言語",
                "自動は英語・スペイン語・フランス語・ドイツ語・ポルトガル語・イタリア語をまとめて読みます。画面がその文字のときは日本語・中国語・韓国語・ロシア語を選んでください。",
                "保存", "保存先:", "参照",
                "範囲を選択", "全画面スクリーンショット", "範囲のOCR",
                "スマート領域: アクティブなウィンドウの文字を強調し、クリックでコピーします",
                "録画", "設定", "最小化", "閉じる",
                "コピー", "保存", "キャンセル",
                "範囲を録画 (FFmpeg)", "全画面を録画 (FFmpeg)", "OBS Studioを起動", "録画を停止",
                "OCRキャプチャ",
                "文字認識には SharpShot の横に言語データが必要です。英語は同梱されています。",
                "アプリのフォルダーを開きますか？"),
            ["ko"] = T(
                "일반", "캡처", "녹화", "대시보드", "모양", "단축키", "업데이트",
                "설정", "개인정보", "언어", "앱 언어",
                "메뉴와 툴팁 언어입니다. 화면 문자 인식 언어는 별도입니다.",
                "OCR 언어",
                "자동은 영어, 스페인어, 프랑스어, 독일어, 포르투갈어, 이탈리아어를 함께 읽습니다. 화면이 그 문자면 일본어, 중국어, 한국어, 러시아어를 선택하세요.",
                "저장", "저장 경로:", "찾아보기",
                "영역 선택", "전체 화면 캡처", "영역 OCR",
                "스마트 영역: 활성 창의 글자를 강조하고, 클릭하면 복사합니다",
                "녹화", "설정", "최소화", "닫기",
                "복사", "저장", "취소",
                "영역 녹화 (FFmpeg)", "전체 화면 녹화 (FFmpeg)", "OBS Studio 실행", "녹화 중지",
                "OCR 캡처",
                "글자 인식에는 SharpShot 옆에 언어 데이터가 필요합니다. 영어는 포함되어 있습니다.",
                "앱 폴더를 열까요?"),
            ["zh-Hans"] = T(
                "常规", "截图", "录制", "面板", "外观", "热键", "更新",
                "设置", "隐私", "语言", "应用语言",
                "更改菜单和提示。屏幕文字识别的语言是单独设置的。",
                "OCR 语言",
                "自动会一起读取英语、西班牙语、法语、德语、葡萄牙语和意大利语。如果屏幕使用其他文字，请选择日语、中文、韩语或俄语。",
                "存储", "保存路径：", "浏览",
                "选择区域", "全屏截图", "区域 OCR",
                "智能区域：高亮当前窗口中的文字，点击即可复制",
                "录制", "设置", "最小化", "关闭",
                "复制", "保存", "取消",
                "录制所选区域 (FFmpeg)", "录制全屏 (FFmpeg)", "启动 OBS Studio", "停止录制",
                "OCR 截图",
                "文字识别需要 SharpShot 旁边的语言数据。已包含英语。",
                "现在打开应用程序文件夹？"),
        };

        private static Dictionary<string, string> T(params string[] values)
        {
            var keys = new[]
            {
                "nav.general", "nav.capture", "nav.recording", "nav.dashboard", "nav.appearance", "nav.hotkeys", "nav.updates",
                "settings.title", "settings.privacy", "settings.language", "settings.appLanguage",
                "settings.appLanguageHint", "settings.ocrLanguage", "settings.ocrLanguageHint",
                "settings.storage", "settings.savePath", "settings.browse",
                "tip.region", "tip.fullscreen", "tip.ocr", "tip.smart", "tip.record", "tip.settings", "tip.minimize", "tip.close",
                "tip.copy", "tip.save", "tip.cancel", "tip.recordRegion", "tip.recordFullscreen", "tip.obs", "tip.stop",
                "ocr.title", "ocr.missing", "ocr.openFolder"
            };
            var map = new Dictionary<string, string>();
            for (int i = 0; i < keys.Length && i < values.Length; i++)
                map[keys[i]] = values[i];
            return map;
        }
    }
}
