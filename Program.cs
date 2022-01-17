using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Elekto.Diagnostics;
using Elekto.Mathematic;

namespace Elekto.Threading.Tasks
{
    internal class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            var sw = Stopwatch.StartNew();

            Console.WriteLine("Iniciando medição de escalabilidade da tarefa.");
            Console.WriteLine("[Esc] para abortar sutilmente.");
            Console.WriteLine("[Ctrl] + [c] para abortar brutalmente.");

            Console.WriteLine();
            Console.WriteLine("Coletando informações da máquina...");
            var machineInfo = GetMachineInfo();
            Console.WriteLine(machineInfo);

            Console.WriteLine();
            var hardDiagnostic = Run(100, 1000);
            if (string.IsNullOrWhiteSpace(hardDiagnostic))
            {
                Console.WriteLine("Bye!");
                return;
            }
            Console.WriteLine();
            Console.WriteLine("Resultados do cálculo pesado:");
            Console.WriteLine(hardDiagnostic);

            Console.WriteLine();
            var lightDiagnostic = Run(25, 20000);
            if (string.IsNullOrWhiteSpace(lightDiagnostic))
            {
                Console.WriteLine("Bye!");
                return;
            }
            Console.WriteLine();
            Console.WriteLine("Resultados do cálculo leve:");
            Console.WriteLine(lightDiagnostic);

            Console.WriteLine();
            Console.WriteLine("Tudo feito em {0:N0}s.\r\n", sw.Elapsed.TotalSeconds);

            var all = "Envie o seguinte para negri@elekto.com.br, por favor:\r\nMaquina:\r\n" + machineInfo +
                      "\r\nTeste pesado:\r\n" + hardDiagnostic + "\r\nTeste leve\r\n" + lightDiagnostic;

            try
            {
                var fileName = Path.Combine(Environment.CurrentDirectory,
                    $"Test.ParallelWork.{DateTime.UtcNow:yyyyMMdd.HHmm}.txt");
                File.WriteAllText(fileName, all, Encoding.UTF8);
                Console.WriteLine("Arquivo de resultados salvo em '{0}'.\r\n", fileName);
                Console.WriteLine("Envie o arquivo, por favor, para negri@elekto.com.br");
            }
            catch (Exception)
            {
                Clipboard.SetText(all);
                Console.WriteLine("Resultados no *Clipboard*. Envie, por favor, para negri@elekto.com.br");
            }

            Clipboard.SetText(all);
            Console.WriteLine("Obrigado por seu tempo!");

            Console.Write("[Enter] para encerrar.");
            Console.ReadLine();
            Console.WriteLine("Bye!");
        }

        private static string Run(int piDigits, int numberOfRepetitions)
        {
            var executionTimesHard = new List<RunResult>();
            for (var i = 0; i < 5; ++i)
            {
                Console.WriteLine();
                Console.WriteLine("Iniciando para {0} digitos de Pi, {1} de 5...", piDigits, i + 1);
                for (var parallelFactor = 0; parallelFactor <= Environment.ProcessorCount*1.5; ++parallelFactor)
                {
                    var workDone = RunPiTask(parallelFactor, piDigits, numberOfRepetitions);
                    if (workDone.Aborted)
                    {
                        return string.Empty;
                    }
                    executionTimesHard.Add(new RunResult(parallelFactor, workDone.TimeTaken.TotalSeconds));
                    Console.WriteLine();
                }
            }
            var averaged = GetAverage(executionTimesHard).ToList();
            return GetDiagnostic(averaged);
        }

        private static LongParallelWork.WorkResult RunPiTask(int parallelFactor, int piDigits, int numberOfRepetitions)
        {
            // Tempo ideal de cada batch, em segundos
            const double batchTime = 1;

            // Tamanho (inicial) de cada batch
            const int batchSize = 10;

            var workDone = LongParallelWork.DoWork(
                i => PiCalculation.GetPi(piDigits), numberOfRepetitions, parallelFactor, batchTime, batchSize,
                (i, ts) =>
                {
                    Console.WriteLine("    Progresso: {0:HH:mm:ss} - Feito {1:N0} em {2:N1}s...", DateTime.Now, i,
                        ts.TotalSeconds);
                    if (Console.KeyAvailable)
                    {
                        var ck = Console.ReadKey(true);
                        return ck.Key != ConsoleKey.Escape;
                    }
                    return true;
                },
                s => Console.WriteLine("    {0}", s));

            return workDone;
        }

        /// <summary>
        ///     Retorna um texto com uma análise da linearidade da tarefa com relação ao numero de CPUs
        /// </summary>
        private static string GetDiagnostic(IEnumerable<RunResult> averaged)
        {
            var runResults = averaged as RunResult[] ?? averaged.ToArray();
            var auto = runResults.First();
            var one = runResults.Skip(1).First();
            var others = runResults.Skip(2);

            var sb = new StringBuilder(" Paralelismo; Tempo (s); Ideal (s); Fator;     Erro Acc\r\n");
            sb.AppendFormat("Auto        ;{0};          ;      ;             \r\n",
                auto.TimeSeconds.ToString("N1").PadLeft(10));
            sb.AppendFormat("           1;{0};          ;      ;             \r\n",
                one.TimeSeconds.ToString("N1").PadLeft(10));

            var accError = 0.0;
            foreach (var run in others)
            {
                double idealFactor = run.CpuCount;
                var idealTime = one.TimeSeconds/idealFactor;
                var realFactor = one.TimeSeconds/run.TimeSeconds;
                var squaredError = (realFactor - idealFactor)*(realFactor - idealFactor);
                accError += squaredError;

                sb.AppendFormat("{0};{1};{2};{3};{4}\r\n",
                    run.CpuCount.ToString(CultureInfo.InvariantCulture).PadLeft(12),
                    run.TimeSeconds.ToString("N1").PadLeft(10),
                    idealTime.ToString("N1").PadLeft(10),
                    realFactor.ToString("N2").PadLeft(6),
                    accError.ToString("N2").PadLeft(13));
            }

            return sb.ToString();
        }

        /// <summary>
        ///     Retorna os tempos médios para cada CPU count, descontando o melhor e o pior tempo
        /// </summary>
        private static IEnumerable<RunResult> GetAverage(IEnumerable<RunResult> executionTimes)
        {
            return (from rr in executionTimes.GroupBy(rr => rr.CpuCount)
                select new RunResult(rr.Key,
                    rr.Select(ct => ct.TimeSeconds).OrderBy(d => d).Skip(1).OrderByDescending(d => d).Skip(1).Average()))
                .OrderBy(rr => rr.CpuCount);
        }

        private static string GetMachineInfo()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("CPU Id:                  ;" + Machine.GetProcessorId());
                sb.AppendLine("64-bit Windows:          ;" + Machine.Is64BitWindows);

                sb.AppendLine("64-bit Process:          ;" + Machine.Is64BitProcess);
                sb.AppendLine("32-bit on 64-bit Windows:;" + Machine.IsWow64Process);
                sb.AppendLine("Number of physical CPU's:;" + Machine.GetPhysicalProcessorCount());
                sb.AppendLine("Number of logical CPU's: ;" + Environment.ProcessorCount);

                sb.AppendLine("                  Relação;            Máscara; Flags");
                foreach (var pi in Machine.GetProcessorsInfo())
                {
                    sb.AppendFormat("{0}; {1}b; 0x{2}\r\n", pi.Relationship.ToString().PadLeft(25),
                        Convert.ToString(pi.ProcessorMask, 2).PadLeft(16, '0').Insert(8, "."),
                        Convert.ToString(pi.Flags, 16));
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine();
                Console.WriteLine("Não foi possivel coletar infomações sobre a CPU da maquina.");
                return string.Empty;
            }
        }

        /// <summary>
        ///     Resultado de uma execução
        /// </summary>
        private class RunResult
        {
            public RunResult(int cpuCount, double timeSeconds)
            {
                CpuCount = cpuCount;
                TimeSeconds = timeSeconds;
            }

            public int CpuCount { get; }
            public double TimeSeconds { get; }
        }
    }
}
