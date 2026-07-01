using MFAAvalonia.Helper.Converters;
using MFAAvalonia.Utilities.Attributes;
using MFAAvalonia.ViewModels;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;

namespace MFAAvalonia.Extensions.MaaFW;

[MaaProperty]
public partial class MaaNode : ViewModelBase
{
    [Browsable(false)] [JsonIgnore] [MaaJsonProperty("name")]
    private string? _name;

    [MaaJsonProperty("recognition")] private string? _recognition;
    [MaaJsonProperty("action")] private string? _action;
    [MaaJsonProperty("next")] [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    private List<string>? _next;
    [MaaJsonProperty("interrupt")] [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    private List<string>? _interrupt;
    [MaaJsonProperty("on_error")] [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    private List<string>? _onError;
    [MaaJsonProperty("is_sub")] private bool? _isSub;
    [MaaJsonProperty("inverse")] private bool? _inverse;
    [MaaJsonProperty("enabled")] private bool? _enabled;
    [MaaJsonProperty("timeout")] private uint _timeout;
    [MaaJsonProperty("pre_delay")] private uint _preDelay;
    [MaaJsonProperty("post_delay")] private uint _postDelay;
    [MaaJsonProperty("rate_limit")] private uint _rateLimit;
    [MaaJsonProperty("pre_wait_freezes")] [JsonConverter(typeof(UIntOrObjectConverter))]
    private object? _preWaitFreezes;
    [MaaJsonProperty("post_wait_freezes")] [JsonConverter(typeof(UIntOrObjectConverter))]
    private object? _postWaitFreezes;
    [MaaJsonProperty("focus")] [JsonConverter(typeof(StringOrBoolOrObjectConverter))]
    private object? _focus;
    // [MaaJsonProperty("focus_tip")] [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    // private List<string>? _focusTip;
    // [MaaJsonProperty("focus_toast")] private string? _focusToast;
    // [MaaJsonProperty("focus_tip_color")] [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    // private List<string>? _focusTipColor;
    // [MaaJsonProperty("focus_succeeded")] [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    // private List<string>? _focusSucceeded;
    // [MaaJsonProperty("focus_succeeded_color")] [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    // private List<string>? _focusSucceededColor;
    // [MaaJsonProperty("focus_failed")] [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    // private List<string>? _focusFailed;
    // [MaaJsonProperty("focus_failed_color")] [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    // private List<string>? _focusFailedColor;
    [MaaJsonProperty("expected")] [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    private List<string>? _expected;
    [MaaJsonProperty("replace")] [JsonConverter(typeof(ReplaceConverter))]
    private List<string[]>? _replace;
    [MaaJsonProperty("only_rec")] private bool? _onlyRec;
    [MaaJsonProperty("labels")] private List<string>? _labels;
    [MaaJsonProperty("model")] private string? _model;
    [MaaJsonProperty("target")] [JsonConverter(typeof(SingleOrNestedListConverter))]
    private object? _target;
    [MaaJsonProperty("target_offset")] [JsonConverter(typeof(SingleOrNestedListConverter))]
    private List<int>? _targetOffset;
    [MaaJsonProperty("begin")] [JsonConverter(typeof(SingleOrNestedListConverter))]
    private object? _begin;
    [MaaJsonProperty("begin_offset")] [JsonConverter(typeof(SingleOrNestedListConverter))]
    private List<int>? _beginOffset;
    [MaaJsonProperty("end")] [JsonConverter(typeof(SingleOrNestedListConverter))]
    private object? _end;
    [MaaJsonProperty("end_offset")] [JsonConverter(typeof(SingleOrNestedListConverter))]
    private List<int>? _endOffset;
    [MaaJsonProperty("starting")] private uint _starting;
    [MaaJsonProperty("duration")] private uint _duration;
    [MaaJsonProperty("key")] [JsonConverter(typeof(GenericSingleOrListConverter<int>))]
    private List<int>? _key;
    [MaaJsonProperty("input_text")] private string? _inputText;
    [MaaJsonProperty("package")] private string? _package;
    [MaaJsonProperty("custom_recognition")]
    private string? _customRecognition;
    [MaaJsonProperty("custom_recognition_param")] [JsonConverter(typeof(StringOrObjectConverter))]
    private object? _customRecognitionParam;
    [MaaJsonProperty("custom_action")] private string? _customAction;
    [MaaJsonProperty("custom_action_param")] [JsonConverter(typeof(StringOrObjectConverter))]
    private object? _customActionParam;
    [MaaJsonProperty("order_by")] private string? _orderBy;
    [MaaJsonProperty("index")] private int _index;
    [MaaJsonProperty("method")] private int _method;
    [MaaJsonProperty("count")] private int _count;
    [MaaJsonProperty("green_mask")] private bool? _greenMask;
    [MaaJsonProperty("detector")] private string? _detector;
    [MaaJsonProperty("ratio")] private double _ratio;
    [MaaJsonProperty("template")] [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    private List<string>? _template;
    [MaaJsonProperty("roi")] [JsonConverter(typeof(SingleOrNestedListConverter))]
    private object? _roi;
    [MaaJsonProperty("roi_offset")] [JsonConverter(typeof(SingleOrNestedListConverter))]
    private object? _roiOffset;
    [MaaJsonProperty("threshold")] [JsonConverter(typeof(GenericSingleOrListConverter<double>))]
    private object? _threshold;
    [MaaJsonProperty("lower")] [JsonConverter(typeof(SingleOrNestedListConverter))]
    private object? _lower;
    [MaaJsonProperty("upper")] [JsonConverter(typeof(SingleOrNestedListConverter))]
    private object? _upper;
    [MaaJsonProperty("connected")] private bool? _connected;
    [MaaJsonProperty("exec")] private string? _exec;
    [MaaJsonProperty("args")] [JsonConverter(typeof(GenericSingleOrListConverter<string>))]
    private List<string>? _args;
    [MaaJsonProperty("detach")] private bool? _detach;

    [Browsable(false)]
    [JsonExtensionData]
    public Dictionary<string, object>? AdditionalData { get; set; } = new();

    public void Merge(MaaNode other)
    {
        foreach (var property in typeof(MaaNode).GetProperties())
        {
            var otherValue = property.GetValue(other);
            if (otherValue != null)
            {
                property.SetValue(this, otherValue);
            }
        }
    }

    public string? ToJson()
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };
        var taskModels = new Dictionary<string, MaaNode>
        {
            [Name ?? "测试"] = this
        };
        return JsonConvert.SerializeObject(taskModels, settings);
    }

    public override string? ToString()
    {
        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        var json = JsonConvert.SerializeObject(this, settings);
        return json;
    }
}
