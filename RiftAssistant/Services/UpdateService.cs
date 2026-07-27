using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Velopack;
using Velopack.Sources;

namespace RiftAssistant.Services
{
    public static class UpdateService
    {
        private const string RepositoryUrl =
            "https://github.com/KazimKarakaya/RiftAssistant";

        private static bool _isChecking;

        public static async Task CheckForUpdatesAsync()
        {
            if (_isChecking)
                return;

            _isChecking = true;

            try
            {
                var source =
                    new GithubSource(
                        RepositoryUrl,
                        accessToken: null,
                        prerelease: true
                    );

                var manager =
                    new UpdateManager(source);

                // Visual Studio / normal dotnet run sırasında uygulama
                // Velopack Setup ile kurulmuş olmayacağı için update deneme.
                if (!manager.IsInstalled)
                {
                    WriteUpdateLog(
                        "[AUTO UPDATE] Atlandı | Uygulama Velopack ile kurulu değil."
                    );

                    return;
                }

                // Daha önce indirilmiş fakat uygulanmamış bir güncelleme varsa
                // önce onu kullanıcıya sun.
                if (manager.UpdatePendingRestart != null)
                {
                    string pendingVersion =
                        manager.UpdatePendingRestart.Version.ToFullString();

                    var pendingResult =
                        MessageBox.Show(
                            $"RiftAssistant v{pendingVersion} güncellemesi hazır.\n\n" +
                            "Şimdi yeniden başlatıp güncellemek ister misin?",
                            "RiftAssistant Güncelleme",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Information
                        );

                    if (pendingResult == MessageBoxResult.Yes)
                    {
                        WriteUpdateLog(
                            $"[AUTO UPDATE] Bekleyen güncelleme uygulanıyor | Version={pendingVersion}"
                        );

                        manager.ApplyUpdatesAndRestart(
                            manager.UpdatePendingRestart
                        );

                        return;
                    }
                }

                WriteUpdateLog(
                    $"[AUTO UPDATE] Kontrol başladı | Current={manager.CurrentVersion?.ToFullString() ?? "Bilinmiyor"}"
                );

                UpdateInfo? update =
                    await manager.CheckForUpdatesAsync();

                if (update == null)
                {
                    WriteUpdateLog(
                        "[AUTO UPDATE] Yeni sürüm yok."
                    );

                    return;
                }

                string newVersion =
                    update.TargetFullRelease.Version.ToFullString();

                WriteUpdateLog(
                    $"[AUTO UPDATE] Yeni sürüm bulundu | Version={newVersion}"
                );

                // Güncellemeyi arka planda tamamen indir.
                await manager.DownloadUpdatesAsync(
                    update,
                    progress =>
                    {
                        // Logu aşırı şişirmemek için yalnızca belirli yüzdelerde yaz.
                        if (progress == 0 ||
                            progress == 25 ||
                            progress == 50 ||
                            progress == 75 ||
                            progress == 100)
                        {
                            WriteUpdateLog(
                                $"[AUTO UPDATE] İndiriliyor | %{progress}"
                            );
                        }
                    }
                );

                WriteUpdateLog(
                    $"[AUTO UPDATE] İndirme tamamlandı | Version={newVersion}"
                );

                var result =
                    MessageBox.Show(
                        $"RiftAssistant v{newVersion} indirildi.\n\n" +
                        "Güncellemeyi tamamlamak için uygulama yeniden başlatılacak.\n" +
                        "Şimdi yeniden başlatılsın mı?",
                        "RiftAssistant Güncelleme",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information
                    );

                if (result != MessageBoxResult.Yes)
                {
                    WriteUpdateLog(
                        $"[AUTO UPDATE] Yeniden başlatma ertelendi | Version={newVersion}"
                    );

                    return;
                }

                WriteUpdateLog(
                    $"[AUTO UPDATE] Güncelleme uygulanıyor | Version={newVersion}"
                );

                manager.ApplyUpdatesAndRestart(
                    update.TargetFullRelease
                );
            }
            catch (Exception ex)
            {
                // Update hatası uygulamanın normal çalışmasını engellemesin.
                WriteUpdateLog(
                    $"[AUTO UPDATE] HATA | {ex}"
                );
            }
            finally
            {
                _isChecking = false;
            }
        }

        private static void WriteUpdateLog(string message)
        {
            try
            {
                string folder =
                    Path.Combine(
                        Environment.GetFolderPath(
                            Environment.SpecialFolder.LocalApplicationData
                        ),
                        "RiftAssistant"
                    );

                Directory.CreateDirectory(folder);

                string path =
                    Path.Combine(
                        folder,
                        "updater.log"
                    );

                File.AppendAllText(
                    path,
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}" +
                    Environment.NewLine
                );
            }
            catch
            {
                // Updater logu yazılamasa bile uygulama çalışmaya devam etsin.
            }
        }
    }
}
