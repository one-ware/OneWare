#nullable enable

namespace OneWare.Core.Data;

public class States
{
    /*public IObservable<bool> QuartusValid { get; }
    
    public IObservable<bool> ArduinoCliValid { get; }

    public IObservable<bool> ActiveVhdpProject { get; }
    
    public IObservable<bool> ActiveArduinoProject { get; }
    
    public IObservable<bool> QuartusValidAndActiveVhdpProject { get; }
    
    public IObservable<bool> QuartusValidAndActiveVhdpProjectAndNios { get; }

    public IObservable<bool> ArduinoCliValidAndActiveArduinoProjectAndNotCompiling { get; }
    
    public States()
    {
        QuartusValid = Global.Options.WhenValueChanged(x => x.QuartusPath)
            .Select(x => Tools.GetQuartusTool(x, "quartus_sh") != null);
        
        ActiveVhdpProject = MainDock.ProjectFiles
            .WhenValueChanged(x => x.ActiveProject)
            .Select(x => x?.Type is FileType.Vhdpproj);
        
        ActiveArduinoProject = MainDock.ProjectFiles
            .WhenValueChanged(x => x.ActiveProject)
            .Select(x => x?.Type is FileType.Arduinoproj);
        
        var activeVhdpNiosProject = MainDock.ProjectFiles
            .WhenValueChanged(x => x.ActiveProject.Properties.SoftwareStartPath)
            .Select(x => x != null);

        var isCompiling = Global.MainWindowViewModel.WhenValueChanged(x => x.IsCompiling);
        
        ArduinoCliValid = Global.Options.WhenValueChanged(x => x.ArduinoCliPath).Select(File.Exists);

        var arduinoCliValidAndActiveArduinoProject =
            ArduinoCliValid.CombineLatest(ActiveArduinoProject, (b, b1) => b && b1);

        ArduinoCliValidAndActiveArduinoProjectAndNotCompiling =
            arduinoCliValidAndActiveArduinoProject.CombineLatest(isCompiling, (b, b1) => b && !b1);
        
        QuartusValidAndActiveVhdpProject = QuartusValid.CombineLatest(ActiveVhdpProject, (b, b1) => b && b1);
        
        QuartusValidAndActiveVhdpProjectAndNios =
            QuartusValidAndActiveVhdpProject.CombineLatest(activeVhdpNiosProject, (b, b1) => b && b1);
    }*/
}