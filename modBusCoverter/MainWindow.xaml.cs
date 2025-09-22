using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Text.Json;
using System.IO;

namespace modBusCoverter
{
    // Class to represent the structure of modbus_config.json
    public class ModbusConfig
    {
        public Dictionary<string, int> MemoryOffsets { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, string> MemoryPrefixes { get; set; } = new Dictionary<string, string>();
    }

    public partial class MainWindow : Window
    {
        private Dictionary<string, int> memoryOffsets = new Dictionary<string, int>();
        private Dictionary<string, string> memoryPrefixes = new Dictionary<string, string>();

        public MainWindow()
        {
            InitializeComponent();
            LoadModbusConfig();
        }

        private void LoadModbusConfig()
        {
            string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modbus_config.json");
            if (File.Exists(configFilePath))
            {
                try
                {
                    string jsonString = File.ReadAllText(configFilePath);
                    ModbusConfig? config = JsonSerializer.Deserialize<ModbusConfig>(jsonString);

                    if (config != null)
                    {
                        memoryOffsets = config.MemoryOffsets;
                        memoryPrefixes = config.MemoryPrefixes;
                    }
                    else
                    {
                        MessageBox.Show("Erro ao carregar a configuração Modbus: O arquivo JSON está vazio ou malformado.", "Erro de Configuração", MessageBoxButton.OK, MessageBoxImage.Error);
                        SetDefaultConfig();
                    }
                }
                catch (JsonException ex)
                {
                    MessageBox.Show($"Erro de sintaxe no arquivo de configuração Modbus: {ex.Message}", "Erro de Configuração", MessageBoxButton.OK, MessageBoxImage.Error);
                    SetDefaultConfig();
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Erro de E/S ao ler o arquivo de configuração Modbus: {ex.Message}", "Erro de Configuração", MessageBoxButton.OK, MessageBoxImage.Error);
                    SetDefaultConfig();
                }
            }
            else
            {
                MessageBox.Show("Arquivo de configuração Modbus (modbus_config.json) não encontrado. Usando configurações padrão.", "Erro de Configuração", MessageBoxButton.OK, MessageBoxImage.Warning);
                SetDefaultConfig();
            }
        }

        private void SetDefaultConfig()
        {
            memoryOffsets = new Dictionary<string, int>
            {
                { "M", 3072 },
                { "V", 512 },
                { "X", 0 },
                { "Y", 1536 },
                { "T", 15360 },
                { "C", 16384 }
            };

            memoryPrefixes = new Dictionary<string, string>
            {
                { "M", "0x" },
                { "V", "4x" },
                { "X", "1x" },
                { "Y", "0x" },
                { "T", "4x" },
                { "C", "4x" }
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var type in memoryOffsets.Keys)
            {
                MemoryTypeComboBox.Items.Add(type);
            }
            MemoryTypeComboBox.SelectedIndex = 0; // Select the first item by default
        }

        private void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            if (MemoryTypeComboBox.SelectedItem == null)
            {
                MessageBox.Show("Por favor, selecione um tipo de memória.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string selectedMemoryType = (string)MemoryTypeComboBox.SelectedItem;
            
            if (!int.TryParse(AddressTextBox.Text, out int inputAddress))
            {
                MessageBox.Show("Por favor, insira um endereço numérico válido.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (memoryOffsets.TryGetValue(selectedMemoryType!, out int offset))
            {
                string prefix = memoryPrefixes.TryGetValue(selectedMemoryType!, out string? p) ? p : "";
                int calculatedAddress = inputAddress + offset;
                ResultTextBox.Text = $"{prefix} {calculatedAddress}";
            }
            else
            {
                MessageBox.Show("Tipo de memória selecionado inválido.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ConvertModbusToPLCButton_Click(object sender, RoutedEventArgs e)
        {
            string modbusAddressInput = ModbusAddressTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(modbusAddressInput))
            {
                MessageBox.Show("Por favor, insira um endereço Modbus.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Parse Modbus address input (e.g., "4x 912")
            string[] parts = modbusAddressInput.Split(' ');
            if (parts.Length != 2)
            {
                MessageBox.Show("Formato de endereço Modbus inválido. Use 'Prefixo Endereço' (ex: 4x 912).", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string inputModbusPrefix = parts[0];
            if (!int.TryParse(parts[1], out int modbusAddressValue))
            {
                MessageBox.Show("Endereço Modbus numérico inválido.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string result = string.Empty;

            if (inputModbusPrefix == "0x")
            {
                // Prioritize M over Y for 0x addresses
                if (memoryOffsets.ContainsKey("M") && modbusAddressValue >= memoryOffsets["M"])
                {
                    int plcAddress = modbusAddressValue - memoryOffsets["M"];
                    result = $"M{plcAddress}";
                }
                else if (memoryOffsets.ContainsKey("Y") && modbusAddressValue >= memoryOffsets["Y"])
                {
                    int plcAddress = modbusAddressValue - memoryOffsets["Y"];
                    result = $"Y{plcAddress}";
                }
            }
            else if (inputModbusPrefix == "1x")
            {
                if (memoryOffsets.ContainsKey("X") && modbusAddressValue >= memoryOffsets["X"])
                {
                    int plcAddress = modbusAddressValue - memoryOffsets["X"];
                    result = $"X{plcAddress}";
                }
            }
            else if (inputModbusPrefix == "4x")
            {
                // Prioritize V, then T, C, M for 4x addresses (common register types)
                if (memoryOffsets.ContainsKey("V") && modbusAddressValue >= memoryOffsets["V"])
                {
                    int plcAddress = modbusAddressValue - memoryOffsets["V"];
                    result = $"V{plcAddress}";
                }
                else if (memoryOffsets.ContainsKey("T") && modbusAddressValue >= memoryOffsets["T"])
                {
                    int plcAddress = modbusAddressValue - memoryOffsets["T"];
                    result = $"T{plcAddress}";
                }
                else if (memoryOffsets.ContainsKey("C") && modbusAddressValue >= memoryOffsets["C"])
                {
                    int plcAddress = modbusAddressValue - memoryOffsets["C"];
                    result = $"C{plcAddress}";
                }
                else if (memoryOffsets.ContainsKey("M") && memoryPrefixes.ContainsKey("M") && memoryPrefixes["M"] == "4x" && modbusAddressValue >= memoryOffsets["M"])
                {
                    int plcAddress = modbusAddressValue - memoryOffsets["M"];
                    result = $"M{plcAddress}";
                }
            }

            if (!string.IsNullOrEmpty(result))
            {
                ResultPLCAddressTextBox.Text = result;
            }
            else
            {
                MessageBox.Show($"Não foi possível converter o endereço Modbus {modbusAddressInput} para um tipo de memória PLC conhecido com o prefixo {inputModbusPrefix}.", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
                ResultPLCAddressTextBox.Text = string.Empty;
            }
        }
    }
}
