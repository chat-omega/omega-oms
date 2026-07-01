using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevExpress.Mvvm;
using ZeroPlus.Oms.Config;

namespace ZeroPlus.Oms.Ui.StartupHelpers;

public class BootstrapConfig : BindableBase
{
    static readonly string configPath = Path.Join(OmsConfig.GetConfigDirectory(), "bootstrap.json");
    static readonly JsonSerializerOptions options = new() {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };
    public bool SingleInstanceOnly { get; set; }
    bool autoUpdateOnStart;
    public bool AutoUpdateOnStart 
    { 
        get => autoUpdateOnStart; 
        set => SetValue(ref autoUpdateOnStart, value); 
    }
    
    public BootstrapConfig(bool useDefault = false)
    {
        PropertyChanged += async (o, e) => { 
            using var fs = new FileStream(configPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(fs, this, typeof(BootstrapConfig), options);
            await fs.FlushAsync();
        };
        if (useDefault)
        {
            autoUpdateOnStart = true;
            SingleInstanceOnly = true;
        }
    }
    
    public static BootstrapConfig LoadUIBoostrapConfig()
    {   
        try 
        {
            using var fs = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return JsonSerializer.Deserialize<BootstrapConfig>(fs) ?? new BootstrapConfig(true);
        }
        catch
        {
            return new BootstrapConfig(true);
        }
    }
}
