namespace OneWare.SoftwareProjectSystem.Templates;

public sealed record SoftwareProjectTemplate(
    string Name,
    string BuildSystem,
    Func<string, IReadOnlyDictionary<string, string>> CreateFiles,
    string? CxxStandard = null,
    string? CStandard = null);

public static class SoftwareProjectTemplates
{
    public const string Empty = "Empty";

    public static IReadOnlyList<SoftwareProjectTemplate> All { get; } =
    [
        new("C++ Hello World", "simple", _ => new Dictionary<string, string>
        {
            ["src/main.cpp"] = """
                               #include <iostream>

                               int main(int argc, char** argv)
                               {
                                   std::cout << "Hello from OneWare!" << std::endl;
                                   return 0;
                               }

                               """
        }, CxxStandard: "c++20"),
        new("C Hello World", "simple", _ => new Dictionary<string, string>
        {
            ["src/main.c"] = """
                             #include <stdio.h>

                             int main(int argc, char** argv)
                             {
                                 printf("Hello from OneWare!\n");
                                 return 0;
                             }

                             """
        }, CStandard: "c17"),
        new("CMake C++ Hello World", "cmake", name => new Dictionary<string, string>
        {
            ["CMakeLists.txt"] = $$"""
                                   cmake_minimum_required(VERSION 3.16)
                                   project({{SanitizeTarget(name)}} LANGUAGES CXX)

                                   set(CMAKE_CXX_STANDARD 20)
                                   set(CMAKE_CXX_STANDARD_REQUIRED ON)

                                   add_executable({{SanitizeTarget(name)}} src/main.cpp)

                                   """,
            ["src/main.cpp"] = """
                               #include <iostream>

                               int main(int argc, char** argv)
                               {
                                   std::cout << "Hello from OneWare!" << std::endl;
                                   return 0;
                               }

                               """
        })
    ];

    /// <summary>
    /// Turns a project name into a valid CMake target / program name.
    /// </summary>
    public static string SanitizeTarget(string name)
    {
        var chars = name.Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' ? c : '_').ToArray();
        var result = new string(chars);
        return result.Length == 0 ? "app" : result;
    }
}
