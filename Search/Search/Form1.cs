using OfficeOpenXml;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Search
{
    public partial class Form1 : Form
    {
        // ���� ���� ��Ʈ
        string rootPath = null;
        Boolean isAddFront = true;
        Boolean isRemoveFront = true;
        string addText = String.Empty;
        int removeTextLength = 0;

        public Form1()
        {
            InitializeComponent();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        #region ���ϸ� �߰�

        // ��Ʈ���
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (sender is TextBox textbox)
            {
                rootPath = textbox.Text;
            }
        }

        // �߰� �ؽ�Ʈ
        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            if (sender is TextBox textbox)
            {
                string inputText = textbox.Text;

                if (ContainsInvalidFileNameChars(inputText))
                {
                    MessageBox.Show("' \\ / : * ? \" < > | ' \r �� ���ڵ��� ���ϸ� ����� �� ���ܴ�^^  ", "���", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    textbox.Text = String.Empty;
                }
                else
                {
                    addText = inputText;
                }
            }
        }

        // �߰� ����
        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                isAddFront = radioButton.Checked;
            }

        }

        // ���ϸ� �߰�
        private void button1_Click(object sender, EventArgs e)
        {
            ProcessFiles(GetNewFileName);
        }

        private string GetNewFileName(string fileName)
        {
            // ���ϸ� ���� ����
            var match = Regex.Match(fileName, @"^(.*)(\.tif(?:\.LDH)?)$");
            if (match.Success)
            {
                if (isAddFront)
                    return $"{addText}{match.Groups[1].Value}{match.Groups[2].Value}";
                else
                    return $"{match.Groups[1].Value}{addText}{match.Groups[2].Value}";
            }
            return fileName;
        }

        #endregion


        #region ���ϸ� ����

        private void textBox3_KeyPress(object sender, KeyPressEventArgs e)
        {
            // ����Ű�� �齺���̽� Ű ���
            if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                e.Handled = true;
            }

            // ù ��° ���ڰ� '0'���� Ȯ��
            if (sender is TextBox textbox && textbox.Text.Length == 0 && e.KeyChar == '0')
            {
                e.Handled = true;
            }
        }
        // ���� �ؽ�Ʈ ����
        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            if (sender is TextBox textbox)
            {
                int length;
                if (int.TryParse(textbox.Text, out length))
                    removeTextLength = length;
                else if (textbox.Text == String.Empty)
                    removeTextLength = 0;
                else
                {
                    MessageBox.Show("����Ÿ���� ���ڸ� �Է��ϰŶ�");
                    return;
                }
            }
        }

        private void radioButton4_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                isRemoveFront = radioButton.Checked;
            }
        }

        // ���ϸ� ����
        private void button2_Click(object sender, EventArgs e)
        {
            ProcessFiles(GetRemovedFileName);
        }

        private string GetRemovedFileName(string fileName)
        {
            // ���ϸ��� Ȯ���ڸ� �и�
            var match = Regex.Match(fileName, @"^(.*)(\.tif(?:\.LDH)?)$");
            if (!match.Success)
                return fileName;  // ������ ���� ������ ���� ���ϸ��� ��ȯ

            string nameWithoutExtension = match.Groups[1].Value;
            string extension = match.Groups[2].Value;

            if (nameWithoutExtension.Length <= removeTextLength)
            {
                MessageBox.Show("�����Ϸ��� ���̰� ���ϸ��� ��ϴ�.");
                return fileName;
            }

            if (isRemoveFront)
            {
                return nameWithoutExtension.Substring(removeTextLength) + extension;
            }
            else
            {
                return nameWithoutExtension.Substring(0, nameWithoutExtension.Length - removeTextLength) + extension;
            }
        }



        #endregion


        #region �Լ�

        private void ProcessFiles(Func<string, string> fileNameModificationFunc)
        {
            try
            {
                if (String.IsNullOrEmpty(rootPath))
                {
                    MessageBox.Show("��Ʈ��θ� �Է��ϰŶ�");
                    return;
                }

                if (!Directory.Exists(rootPath))
                {
                    MessageBox.Show("������ ��ΰ� �������� �ʱ���");
                    return;
                }

                var result = new ConcurrentBag<(string FolderName, string OldFileName, string NewFileName)>();

                ProcessDirectoryFiles(rootPath, fileNameModificationFunc, result);

                var directories = Directory.GetDirectories(rootPath);


                Parallel.ForEach(directories, dir =>
                {
                    ProcessDirectoryFiles(dir, fileNameModificationFunc, result);
                });

                var sortedResult = result.OrderBy(r => r.FolderName).ThenBy(r => r.OldFileName).ToList();

                // ListBox�� ��� ǥ��
                listBox1.Invoke(new Action(() =>
                {
                    listBox1.Items.Clear();
                    foreach (var item in sortedResult)
                    {
                        listBox1.Items.Add($"������: {item.FolderName},  ���� ���ϸ�: {item.OldFileName},  ������ ���ϸ�: {item.NewFileName}");
                    }
                }));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"����: {ex.Message}");
            }
        }

        // ��Ʈ ���丮�� ���� ó���ؾ���
        private void ProcessDirectoryFiles(string directoryPath, Func<string, string> fileNameModificationFunc, ConcurrentBag<(string FolderName, string OldFileName, string NewFileName)> result)
        {
            var files = Directory.GetFiles(directoryPath, "*.*");
            if (files.Length == 0)
            {
                result.Add((Path.GetFileName(directoryPath), "No Files", "No Files"));
            }
            else
            {
                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string newFileName = fileNameModificationFunc(fileName);
                    if (newFileName != fileName)
                    {
                        string newPath = Path.Combine(directoryPath, newFileName);
                        File.Move(file, newPath);
                        result.Add((Path.GetFileName(directoryPath), fileName, newFileName));
                    }
                }
            }
        }

        // ������ ���ϸ� ������ Ư������ Ȯ��
        // \ / : * ? " < > |
        private bool ContainsInvalidFileNameChars(string text)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in text)
            {
                if (invalidChars.Contains(c))
                {
                    return true;
                }
            }
            return false;
        }
        #endregion







    }
}
