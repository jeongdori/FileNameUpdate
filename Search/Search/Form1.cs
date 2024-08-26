using OfficeOpenXml;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Search
{
    public partial class Form1 : Form
    {
        // 원본 파일 루트
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

        #region 파일명 추가

        // 루트경로
        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (sender is TextBox textbox)
            {
                rootPath = textbox.Text;
            }
        }

        // 추가 텍스트
        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            if (sender is TextBox textbox)
            {
                string inputText = textbox.Text;

                if (ContainsInvalidFileNameChars(inputText))
                {
                    MessageBox.Show("' \\ / : * ? \" < > | ' \r 이 문자들은 파일명에 사용할 수 없단다^^  ", "경고", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    textbox.Text = String.Empty;
                }
                else
                {
                    addText = inputText;
                }
            }
        }

        // 추가 라디오
        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            if (sender is RadioButton radioButton)
            {
                isAddFront = radioButton.Checked;
            }

        }

        // 파일명 추가
        private void button1_Click(object sender, EventArgs e)
        {
            ProcessFiles(GetNewFileName);
        }

        private string GetNewFileName(string fileName)
        {
            // 파일명 수정 로직
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


        #region 파일명 제거

        private void textBox3_KeyPress(object sender, KeyPressEventArgs e)
        {
            // 숫자키와 백스페이스 키 허용
            if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                e.Handled = true;
            }

            // 첫 번째 문자가 '0'인지 확인
            if (sender is TextBox textbox && textbox.Text.Length == 0 && e.KeyChar == '0')
            {
                e.Handled = true;
            }
        }
        // 제거 텍스트 길이
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
                    MessageBox.Show("정수타입의 숫자만 입력하거라");
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

        // 파일명 제거
        private void button2_Click(object sender, EventArgs e)
        {
            ProcessFiles(GetRemovedFileName);
        }

        private string GetRemovedFileName(string fileName)
        {
            // 파일명에서 확장자를 분리
            var match = Regex.Match(fileName, @"^(.*)(\.tif(?:\.LDH)?)$");
            if (!match.Success)
                return fileName;  // 형식이 맞지 않으면 원래 파일명을 반환

            string nameWithoutExtension = match.Groups[1].Value;
            string extension = match.Groups[2].Value;

            if (nameWithoutExtension.Length <= removeTextLength)
            {
                MessageBox.Show("제거하려는 길이가 파일명보다 깁니다.");
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


        #region 함수

        private void ProcessFiles(Func<string, string> fileNameModificationFunc)
        {
            try
            {
                if (String.IsNullOrEmpty(rootPath))
                {
                    MessageBox.Show("루트경로를 입력하거라");
                    return;
                }

                if (!Directory.Exists(rootPath))
                {
                    MessageBox.Show("지정된 경로가 존재하지 않구나");
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

                // ListBox에 결과 표시
                listBox1.Invoke(new Action(() =>
                {
                    listBox1.Items.Clear();
                    foreach (var item in sortedResult)
                    {
                        listBox1.Items.Add($"폴더명: {item.FolderName},  원본 파일명: {item.OldFileName},  수정된 파일명: {item.NewFileName}");
                    }
                }));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류: {ex.Message}");
            }
        }

        // 루트 디렉토리의 파일 처리해야함
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

        // 윈도우 파일명에 금지된 특수문자 확인
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
