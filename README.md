 
![logo_plugfy_core_foundation_256x55](https://github.com/user-attachments/assets/a03e7fde-dcf1-42be-8c10-2922996f39c4)

# Plugfy Core Extension Library Runner for .NET

## Overview
The **Plugfy Core Extension Library Runner for .NET** is a command-line tool designed to dynamically load and execute .NET assemblies. It facilitates inter-process communication and allows users to list available assemblies, obtain detailed metadata, and execute methods within assemblies.

This tool is part of the **Plugfy Core** ecosystem, providing a standardized approach to managing dynamic module execution in .NET applications.

---

## Features
- **List Available Assemblies**: Retrieve a list of all dynamically loadable assemblies.
- **Get Assembly Metadata**: Extract class, method, event, and field details from assemblies.
- **Execute Methods Dynamically**: Invoke methods within loaded assemblies, supporting constructor parameters and runtime event subscription.
- **Interactive Mode**: Run the tool interactively for real-time command execution.
- **Multiple Communication Modes**: Supports different inter-process communication mechanisms (e.g., STDInOut, NamedPipes).
- **Extensible Logging and Caller Communication**: Configurable logging and communication extensions via JSON parameters.

---

## Installation
### **Prerequisites**
- .NET SDK 6.0 or later
- Windows, Linux, or macOS

### **Building the Project**
Clone the repository and navigate to the project folder:
```sh
 git clone https://github.com/plugfy/plugfy-core-extension-library-runner-dotnet.git
 cd plugfy-core-extension-library-runner-dotnet
```

Build the project using .NET CLI:
```sh
 dotnet build -c Release
```

Run the executable from the output directory:
```sh
 dotnet run -- list
```

---

## Usage
### **Command Structure**
The tool uses a **command-based interface** where commands are specified as verbs:
```sh
 Plugfy.Core.Extension.Library.Runner.DotNet8 <command> [options]
```

### **Available Commands**
#### **1. Listing Available Assemblies**
```sh
 Plugfy.Core.Extension.Library.Runner.DotNet8 list
```
**Options:**
- `-t, --communicationType` *(optional)*: Communication method (e.g., `STDInOut`, `NamedPipes`)
- `-i, --interactive` *(optional)*: Enables interactive mode
- `-c, --caller` *(optional)*: Caller extensions configuration in JSON format
- `-l, --log` *(optional)*: Log extensions configuration in JSON format

#### **2. Getting Assembly Information**
```sh
 Plugfy.Core.Extension.Library.Runner.DotNet8 info '{"AssemblyName": "MyLibrary.dll"}'
```
**Options:**
- `-t, --communicationType` *(optional)*: Communication method
- `-i, --interactive` *(optional)*: Enables interactive mode
- `-c, --caller` *(optional)*: Caller extensions configuration in JSON format
- `-l, --log` *(optional)*: Log extensions configuration in JSON format

#### **3. Running a Method in an Assembly**
```sh
 Plugfy.Core.Extension.Library.Runner.DotNet8 run '{"AssemblyName": "MyLibrary.dll", "Class": "MyNamespace.MyClass", "Method": "MyMethod", "Parameters": ["arg1", 42]}'
```
**Options:**
- `-t, --communicationType` *(optional)*: Communication method
- `-i, --interactive` *(optional)*: Enables interactive mode
- `-c, --caller` *(optional)*: Caller extensions configuration in JSON format
- `-l, --log` *(optional)*: Log extensions configuration in JSON format

---

## Configuration
The application relies on `appsettings.json` for configuration. Below is an example:
```json
{
  "Extensions": {
    "Communications": {
      "STDInOut": {
        "LibrariesPath": "./Extensions/STDInOut"
      },
      "NamedPipes": {
        "LibrariesPath": "./Extensions/NamedPipes"
      }
    }
  },
  "LibrariesPath": "./Assemblies"
}
```
### **Customizing Communication Extensions**
- Define additional communication types in `appsettings.json`.
- Ensure the corresponding `.dll` exists in the specified `LibrariesPath`.

---

## Example Workflows
### **Example 1: Listing Assemblies**
```sh
Plugfy.Core.Extension.Library.Runner.DotNet8 list -t NamedPipes
```
### **Example 2: Retrieving Assembly Information**
```sh
Plugfy.Core.Extension.Library.Runner.DotNet8 info '{"AssemblyName": "MyLibrary.dll"}'
```
### **Example 3: Executing a Method in an Assembly**
```sh
Plugfy.Core.Extension.Library.Runner.DotNet8 run '{"AssemblyName": "MyLibrary.dll", "Class": "MyNamespace.MyClass", "Method": "SayHello", "Parameters": ["World"]}'
```

---

## License
This project is licensed under the **GNU General Public License v3.0**. See [GNU GPL v3.0](https://www.gnu.org/licenses/gpl-3.0.en.html) for details.

---

## Contributing
We welcome contributions! To contribute:
1. Fork the repository.
2. Create a new feature branch (`git checkout -b feature-new`).
3. Commit changes (`git commit -m "Added new feature"`).
4. Push to the branch (`git push origin feature-new`).
5. Submit a Pull Request.

For major changes, open an issue first to discuss the proposed changes.

---

## Contact
For inquiries, feature requests, or issues, please open a GitHub issue or contact the **Plugfy Foundation**.

