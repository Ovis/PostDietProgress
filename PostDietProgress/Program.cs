﻿using Newtonsoft.Json;
using PostDietProgress.Model;
using PostDietProgress.Service;
using System;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace PostDietProgress
{
    public static class Program
    {
        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        static async Task Main(string[] args)
        {
            var setting = new Settings();

            var httpClient = new HttpClient();
            var handler = new HttpClientHandler() { UseCookies = true };
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "ja-JP");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            var dbSvs = new DatabaseService(setting);

            /* エンコードプロバイダーを登録(Shift-JIS用) */
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var healthPlanetSvs = new HealthPlanetService(httpClient, handler, dbSvs, setting);

            switch (args.Length)
            {
                case 0:
                    {
                        var discordService = new DiscordService(setting, httpClient, dbSvs, healthPlanetSvs);

                        /* エラーフラグ確認 */
                        var errorFlag = await dbSvs.GetSettingDbVal(SettingDbEnum.ErrorFlag);

                        switch (errorFlag)
                        {
                            case "1":
                                await healthPlanetSvs.GetRefreshToken();
                                try
                                {
                                    JsonConvert.DeserializeObject<InnerScan>(await healthPlanetSvs.GetHealthDataAsync());
                                }
                                catch
                                {
                                    await discordService.SendDiscordAsync("トークンを更新しましたが、データの取得に失敗しました。");
                                    await dbSvs.SetSettingDbVal(SettingDbEnum.ErrorFlag, "2");
                                    await dbSvs.SetSettingDbVal(SettingDbEnum.PreviousErrorDateTime, setting.LocalTime.ToString());
                                    throw;
                                }
                                await dbSvs.SetSettingDbVal(SettingDbEnum.ErrorFlag, "0");
                                return;
                            case "2":
                                DateTime.TryParseExact(await dbSvs.GetSettingDbVal(SettingDbEnum.PreviousErrorDateTime)
                                    , "yyyyMMddHHmm",
                                    new CultureInfo("ja-JP"),
                                    DateTimeStyles.AssumeLocal, out var errorDate);

                                if (errorDate <= setting.LocalTime.AddHours(-3)) return;

                                await healthPlanetSvs.GetRefreshToken();
                                try
                                {
                                    JsonConvert.DeserializeObject<InnerScan>(await healthPlanetSvs.GetHealthDataAsync());
                                }
                                catch
                                {
                                    await dbSvs.SetSettingDbVal(SettingDbEnum.PreviousErrorDateTime, setting.LocalTime.ToString());
                                    throw;
                                }
                                await dbSvs.SetSettingDbVal(SettingDbEnum.ErrorFlag, "0");
                                return;
                        }

                        /* 前回測定日時 */
                        var previousDate = await dbSvs.GetSettingDbVal(SettingDbEnum.PreviousMeasurementDate);

                        if (!string.IsNullOrEmpty(previousDate))
                        {
                            DateTime.TryParseExact(previousDate, "yyyyMMddHHmm", new CultureInfo("ja-JP"), DateTimeStyles.AssumeLocal, out var prevDate);
                            if (prevDate > setting.LocalTime.AddHours(-6))
                            {
                                return;
                            }
                        }

                        //リクエストトークン取得
                        await healthPlanetSvs.GetHealthPlanetToken();

                        /* 身体データ取得 */
                        InnerScan healthData;
                        try
                        {
                            healthData = JsonConvert.DeserializeObject<InnerScan>(await healthPlanetSvs.GetHealthDataAsync());
                        }
                        catch
                        {
                            await discordService.SendDiscordAsync("身体データの取得に失敗しました。");
                            await dbSvs.SetSettingDbVal(SettingDbEnum.ErrorFlag, "1");
                            throw;
                        }

                        var healthList = healthData.Data;

                        /* 最新の日付のデータを取得 */
                        healthList.Sort((a, b) => string.CompareOrdinal(b.Date, a.Date));
                        var latestDate = healthList.First().Date;

                        if (latestDate.Equals(previousDate))
                        {
                            //前回から計測日が変わっていない(=計測してない)ので処理を終了
                            return;
                        }

                        /* Discordに送るためのデータをDictionary化 */
                        var health = new HealthData(latestDate, healthList.Where(x => x.Date.Equals(latestDate)).Select(x => x).ToDictionary(x => x.Tag, x => x.Keydata));

                        /* Discordに送信 */
                        var sendData = await discordService.CreateSendDataAsync(health, healthData.Height, latestDate);
                        await discordService.SendDiscordAsync(sendData);

                        if (setting.PostGoogleFit)
                        {
                            var googleFitService = new GoogleFitService(setting, httpClient);
                            await googleFitService.PostGoogleFit(health);
                        }

                        /* 前回情報をDBに登録 */
                        await dbSvs.SetHealthData(latestDate, health);
                        break;
                    }
                case 2:
                    {
                        var userId = args[0];
                        var passwd = args[1];
                        //初期処理
                        await dbSvs.CreateTable();
                        await healthPlanetSvs.OAuthProcessAsync(userId, passwd);
                        await dbSvs.SetSettingDbVal(SettingDbEnum.ErrorFlag, "0");

                        //GoogleAPI処理
                        if (setting.PostGoogleFit)
                        {
                            var googleFitService = new GoogleFitService(setting, httpClient);
                            await googleFitService.GetGoogleOAuth();
                        }

                        Console.WriteLine("初期処理が完了しました。");
                        return;
                    }
                default:
                    Console.WriteLine("引数の個数が誤っています。");
                    break;
            }
        }
    }
}
