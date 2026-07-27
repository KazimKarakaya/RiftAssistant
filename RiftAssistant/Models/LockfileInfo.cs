using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiftAssistant.Models;

public class LockfileInfo
{
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public int Port { get; set; }
    public string Password { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
}
