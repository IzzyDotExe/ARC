using Microsoft.Extensions.Configuration;

namespace ArcV3;
using DSharpPlus;

public class Arc {

  public Arc() {
    
  }

  public void Run() {

    var settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");

    if (!File.Exists(settingsPath))
      throw new FileNotFoundException("Could not find the file config.json ~ Please place your config.json in the working directory of ARC");

    var config = new ConfigurationBuilder()
      .AddJsonFile(settingsPath)
      .Build();

    AsyncRun(config).GetAwaiter().GetResult();

  }


  private async Task AsyncRun(IConfigurationRoot config) {

  
    
  }


}

