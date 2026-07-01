using MaaFramework.Binding;
using MaaFramework.Binding.Custom;
using MFAAvalonia.Helper;
using System;
using System.Net.Http;
using System.Text;

namespace MFAAvalonia.Extensions.MaaFW.Custom;

public class WebhookAction : IMaaCustomAction
{
    public string Name { get; set; } = nameof(WebhookAction);

    public bool Run<T>(T context, in RunArgs args, in RunResults results) where T : IMaaContext
    {
        try
        {
            ActionParamHelper.ThrowIfStopping(context);

            var url = "";
            var method = "POST";
            var body = "";
            var contentType = "application/json";

            if (!string.IsNullOrWhiteSpace(args.ActionParam))
            {
                var json = ActionParamHelper.Parse(args.ActionParam);
                url = (string?)json["url"] ?? "";
                method = (string?)json["method"] ?? "POST";
                body = (string?)json["body"] ?? "";
                contentType = (string?)json["content_type"] ?? "application/json";
            }

            if (string.IsNullOrWhiteSpace(url))
            {
                LoggerHelper.Warning("执行 Webhook 失败：未配置 URL");
                return false;
            }

            LoggerHelper.Info($"开始执行 Webhook：method={method}, url={url}, contentType={contentType}, bodyLength={body.Length}");
            using var client = VersionChecker.CreateHttpClientWithProxy();
            client.Timeout = TimeSpan.FromSeconds(30);

            HttpResponseMessage response;
            if (method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                response = ActionParamHelper.SendHttpWithStopCheck(context, token => client.GetAsync(url, token));
            }
            else
            {
                var content = new StringContent(body, Encoding.UTF8, contentType);
                response = ActionParamHelper.SendHttpWithStopCheck(context, token => client.PostAsync(url, content, token));
            }

            LoggerHelper.Info($"Webhook 响应：status={(int)response.StatusCode} {response.StatusCode}, success={response.IsSuccessStatusCode}");
            return response.IsSuccessStatusCode;
        }
        catch (MaaStopException)
        {
            LoggerHelper.Info("执行 Webhook 时检测到手动停止，已中止请求");
            return false;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"执行 Webhook 异常：{e.Message}", e);
            return false;
        }
    }
}
