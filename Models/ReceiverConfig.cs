namespace tagless_receiver.Models;

public class ReceiverConfig
{
    public string receiver_device_id { get; set; } = "";

    public string app_version { get; set; } = "1.0.0";

    public int? business_id { get; set; }

    public int? class_id { get; set; }

    public int? staff_id { get; set; }

    public bool registered { get; set; } = false;

    public int config_version { get; set; } = 1;
}