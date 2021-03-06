﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using NPlot;

namespace OCR
{
    public partial class Form1 : Form
    {
        #region Field
        private NeuralNetwork nn;
        private double[] inferResult;
        private double[][] sourceData;
        private double[][] inAry;
        private double[][] outAry;
        private double[] errorAry;
        private List<string> charList;
        private List<string[]> charData;
        private Thread _workerThread;
        #endregion

        #region Properties
        public double[][] SourceData
        {
            get
            {
                return sourceData;   
            }
            set
            {
                sourceData = value;
            }
        }
        #endregion

        public Form1()
        {
            InitializeComponent();
            //SubscribeChangedTxt();
        }

        private void SubscribeChangedTxt()
        {
            for (int i = 0; i < this.Controls.Count; i++)
            {
                if (this.Controls[i] is TextBox)
                {
                    txt_Weight.Text += ((TextBox)this.Controls[i]).Name + "\r\n";
                }
                else if (this.Controls[i] is GroupBox)
                {
                    for (int j = 0; j < ((GroupBox)this.Controls[i]).Controls.Count; j++)
                    {
                        if (((GroupBox)this.Controls[i]).Controls[j] is TextBox)
                        {
                            txt_Weight.Text += ((GroupBox)this.Controls[i]).Controls[j].Name + "\r\n";
                        }
                    }
                    
                }
            }
        }

        #region Button
        private void btn_Open_Click(object sender, EventArgs e)
        {
            StreamReader reader = null;

            OpenFileDialog ofd = new OpenFileDialog();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                txt_FilePath.Text = ofd.FileName;
            }
            else
            {
                return;
            }

            tssl_Status.Text = "Loading file...";
            if (File.Exists(txt_FilePath.Text))
            {
                reader = new StreamReader(txt_FilePath.Text);
            }

            // Create List
            charList = new List<string>();
            charData = new List<string[]>();

            string tempLine = "";
            string tempText = "";
            while (!reader.EndOfStream)
            {
                tempLine = reader.ReadLine().Trim();
                if (tempLine.Length == 2)
                {
                    if (tempText != "")
                    {
                        charData.Add(tempText.Substring(0, tempText.Length - 1).Split(','));
                        tempText = "";  //reset
                    }
                    charList.Add(tempLine[0].ToString());
                }
                else
                {
                    tempText += tempLine + ",";
                }
            }
            // End of file, last check
            if (tempText != "")
            {
                charData.Add(tempText.Substring(0, tempText.Length - 1).Split(','));
                tempText = "";
            }
            reader.Close();

            // Refresh cmb_InputChar
            cmb_InputChar.Items.Clear();
            foreach (string s in charList)
            {
                cmb_InputChar.Items.Add(s);
            }

            // Assign value to input and output array
            SourceData = null;  //reset
            SourceData = new double[charData.Count][];
            inAry = null;   //reset
            outAry = null;  //reset
            inAry = new double[charData.Count][];
            outAry = new double[charList.Count][];

            string temp = "";
            for (int i = 0; i < charData.Count; i++)
            {
                temp = "";  //reset
                SourceData[i] = new double[charData[i].Length * charData[i][0].Length];
                for (int j = 0; j < charData[i].Length; j++)
                {
                    temp += charData[i][j];
                }
                for (int k = 0;k<temp.Length;k++)
                {
                    sourceData[i][k] = Convert.ToDouble(temp[k].ToString());
                }
            }
            for (int i = 0; i < charList.Count; i++)
            {
                temp = "";  //reset
                outAry[i] = new double[26];
                outAry[i][i] = 1;
            }

            // Set input data
            inAry = new double[SourceData.Length][];
            for (int i = 0; i < inAry.Length; i++)
            {
                inAry[i] = new double[SourceData[i].Length];
                Array.Copy(SourceData[i], inAry[i], SourceData[i].Length);
            }

            tssl_Status.Text = "Finished.";
        }

        private void btn_Train_Click(object sender, EventArgs e)
        {
            if (inAry == null)
            {
                MessageBox.Show("Please load in data.");
                return;
            }
            if (nn != null)
            {
                // TODO: delete this if it's unnecessary
            }
            tssl_Status.Text = "Training...";

            _workerThread = new Thread(StartTraining);
            _workerThread.Start();
            _workerThread.Join();

            PlotErrorCurve(errorAry);
            tssl_Status.Text = "Training is done.";
            
        }

        private void btn_DeleteCurve_Click(object sender, EventArgs e)
        {
            if (plotSurface.Drawables.Count == 0)
            {
                return;
            }
            else
            {
                IDrawable p = plotSurface.Drawables[plotSurface.Drawables.Count - 1] as IDrawable;
                plotSurface.Remove(p, false);
                plotSurface.Refresh();
            }
        }

        private void btn_Infer_Click(object sender, EventArgs e)
        {
            // Check that neural network has been created
            if (nn == null)
            {
                MessageBox.Show("Neural network has not been created.");
                return;
            }
            // Check that character data has been loaded in
            if (charData.Count == 0)
            {
                MessageBox.Show("Please load character data in.");
                return;
            }
            if (cmb_InputChar.SelectedIndex == -1)
            {
                MessageBox.Show("Please select an input charater.");
                return;
            }

            // Start inferencing
            inferResult = null;     // Reset
            inferResult = new double[charList.Count];
            nn.Infer(ref inAry[cmb_InputChar.SelectedIndex], ref inferResult);

            // Show recognition result
            txt_Result.Clear();
            txt_Result.Text += "Char: " + charList[inferResult.ArgMax()] + "\r\n";
            ShowResult(ref inferResult);

            // Show network weight
            txt_NetworkOutput.Clear();
            for (int i = 0; i < inferResult.Length; i++)
            {
                txt_NetworkOutput.Text += inferResult[i].ToString("F6") + "\r\n";
            }
        }

        private void btn_AddNoise_Click(object sender, EventArgs e)
        {
            if (txt_NoisePercentage.Text == "")
            {
                MessageBox.Show("Please set noise percentage first.");
                return;
            }

            double noisePercentage;
            bool canConvert = double.TryParse(txt_NoisePercentage.Text, out noisePercentage);
            if (!canConvert)
            {
                MessageBox.Show("Please check that is there any non-numeric character in the textbox.");
                return;
            }
            noisePercentage /= 100.0;   // Convert to decimal number

            if (noisePercentage == 0)
            {
                // Reset (copy the source data into inAry)
                for (int i = 0; i < SourceData.Length; i++)
                {
                    Array.Copy(SourceData[i], inAry[i], SourceData[i].Length);
                }
            }
            else
            {
                for (int i = 0; i < SourceData.Length; i++)
                {
                    Array.Copy(SourceData[i], inAry[i], SourceData[i].Length);
                    AddNoise(ref inAry[i], noisePercentage);
                }
            }

            txt_InputChar.Clear();
            cmb_InputChar.SelectedIndex = -1;
            tssl_Status.Text = "Noise has been added in charData.";
        }
        #endregion

        #region Other controls
        private void cmb_InputChar_SelectedIndexChanged(object sender, EventArgs e)
        {
            txt_InputChar.Clear();
            int idx;
            if (cmb_InputChar.SelectedIndex != -1)
            {
                idx = cmb_InputChar.SelectedIndex;
                for (int i = 0; i < charData[idx].Length; i++)
                {
                    for (int j = 0; j < charData[idx][i].Length; j++)
                    {
                        txt_InputChar.Text += inAry[idx][i*8 + j] == 1 ? "■" : "□";
                    }
                    txt_InputChar.Text += "\r\n";
                }

                // Auto-inference
                if (chk_AutoInfer.Checked && nn != null)
                    btn_Infer_Click(null, null);
            }
        }
        #endregion

        #region Private function
        private void ShowWeight(ref NeuralNetwork nn)
        {
            txt_Weight.Clear();
            for (int i = 0; i < nn.Neurons.Length; i++)
            {
                txt_Weight.Text += "Layer_" + i.ToString() + ":\r\n";
                for (int j = 0; j < nn.Neurons[i].Length; j++)
                {
                    txt_Weight.Text += "Neuron_" + j.ToString() + ":\r\n";
                    txt_Weight.Text += "(";
                    for (int k = 0; k < nn.Neurons[i][j].Weight.Length; k++)
                    {
                        txt_Weight.Text += nn.Neurons[i][j].Weight[k].ToString("F4");
                        if (k < nn.Neurons[i][j].Weight.Length - 1)
                        {
                            txt_Weight.Text += ",";
                        }
                    }
                    txt_Weight.Text += ") \r\n";
                }
                txt_Weight.Text += "\r\n";
            }
        }

        private void PlotErrorCurve(double[] errorVector)
        {
            double[] x = new double[errorVector.Length];

            for (int i = 0; i < x.Length; i++)
            {
                x[i] = i;
            }

            LinePlot lp = new LinePlot();
            lp.OrdinateData = errorVector;
            lp.AbscissaData = x;
            lp.Pen = new Pen(Color.Red, 2.0f);
            plotSurface.Add(lp);
            plotSurface.Title = "Convergence curve";
            plotSurface.XAxis1.Label = "Epoch";
            plotSurface.YAxis1.Label = "Error";

            Legend legend = new Legend();
            legend.AttachTo(PlotSurface2D.XAxisPosition.Top, PlotSurface2D.YAxisPosition.Left);
            legend.VerticalEdgePlacement = Legend.Placement.Inside;
            legend.HorizontalEdgePlacement = Legend.Placement.Inside;
            legend.YOffset = 8;
            plotSurface.Legend = legend;
            plotSurface.Refresh();
        }

        private void StartTraining()
        {
            int epoch = Convert.ToInt32(txt_Epoch.Text);
            double etta = Convert.ToDouble(txt_Etta.Text);
            double alpha = Convert.ToDouble(txt_Alpha.Text);
            double error = Convert.ToDouble(txt_Error.Text);

            string[] temp = txt_Neurons.Text.Split(',');
            int[] neuronsInEachLayer = new int[temp.Length];
            for (int i = 0; i < temp.Length; i++)
            {
                neuronsInEachLayer[i] = Convert.ToInt32(temp[i]);
            }

            // Create error array
            errorAry = new double[epoch];

            nn = new NeuralNetwork(neuronsInEachLayer);
            nn.Train(ref inAry, ref outAry, ref errorAry, epoch, error, etta, alpha);
        }

        private void AddNoise(ref double[] data, double noisePercentage)
        {
            Random rand = new Random();
            int noiseGrainAmount = (int)(noisePercentage*data.Length);
            int[] idxAry = new int[data.Length];

            // Initialization
            idxAry.Fill(0, idxAry.Length);

            idxAry.Shuffle(noiseGrainAmount);

            for (int i = 0; i < noiseGrainAmount; i++)
            {
                data[idxAry[i]] = data[idxAry[i]] == 1 ? 0 : 1;
            }
        }

        private void ShowResult(ref double[] result)
        {
            int[] sortedIdxAry = new int[result.Length];
            double sum = 0;
            double[] temp = new double[3];
            double[] sortedAry = new double[result.Length];

            // Initialize index array
            sortedIdxAry.Fill(0, sortedIdxAry.Length);

            // Copy array to prevent modifying orignal data
            Array.Copy(result, sortedAry, result.Length);

            // Do quick sort
            ArrayOps.QuickSort(ref sortedAry, ref sortedIdxAry, 0, sortedAry.Length - 1);

            // Show the fisrt several maximum (format: `character: probability`)
            sum = result.Sum();
            int k = 0;
            double tempSum = 0;
            while (k < 3 || (k < 5 && tempSum <= 0.9))
            {
                txt_Result.Text += charList[sortedIdxAry[k]] + ": " + (sortedAry[k] / sum * 100).ToString("F4") + "%\r\n";
                tempSum += sortedAry[k] / sum;
                k++;
            }
        }
        #endregion

        private void btn_ShowWeight_Click(object sender, EventArgs e)
        {
            if (nn == null)
                return;

            ShowWeight(ref nn);
        }
    }
}