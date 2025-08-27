using StdBE100;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;


namespace POS.Encomendas.JTA.POS
{
    public partial class EditorEscolherEncomendas : Form
    {

        private ErpBS100.ErpBS _BSO;
        private StdPlatBS100.StdBSInterfPub _PSO;
        private string _Cliente;
        private HashSet<string> _encomendasUsadas;
        private List<DataGridViewRow> _dadosOriginais = new List<DataGridViewRow>();
        private Dictionary<string, decimal> _descontosEntidade = new Dictionary<string, decimal>();

        public List<string> EncomendasSelecionadas { get; private set; } = new List<string>();




        public EditorEscolherEncomendas(ErpBS100.ErpBS bSO, StdPlatBS100.StdBSInterfPub pSO, string clientePOS, HashSet<string> encomendasUsadas)
        {
            InitializeComponent();
            _BSO = bSO;
            _PSO = pSO;
            _Cliente = clientePOS;
            _encomendasUsadas = encomendasUsadas;
        }

        private void EditorEscolherEncomendas_Load(object sender, EventArgs e)
        {
            // Configurar datas padrão (6 meses atrás até hoje)
            dateTimePicker_inicio.Value = DateTime.Now.AddMonths(-6);
            dateTimePicker_fim.Value = DateTime.Now;

            var dadosCabecDocs = GetCabecDocs(_Cliente);
            CarregarDadosDataGrid(dadosCabecDocs);
            txt_nomeCliente.Text = _Cliente;
            PopularComboBoxes();
        }

        private void CarregarDadosDataGrid(StdBELista dadosCabecDocs)
        {
            dadosCabecDocs.Inicio();
            dataGridView1.Rows.Clear();
            _dadosOriginais.Clear();

            while (!dadosCabecDocs.NoFim())
            {
                string id = dadosCabecDocs.DaValor<string>("Id");
                string tipoDoc = dadosCabecDocs.DaValor<string>("TipoDoc");
                string numDoc = dadosCabecDocs.DaValor<string>("NumDoc");
                string serie = dadosCabecDocs.DaValor<string>("Serie");
                string entidade = dadosCabecDocs.DaValor<string>("Entidade");
                decimal descEntidade = dadosCabecDocs.DaValor<decimal>("DescEntidade");

                // Armazenar o desconto da entidade para uso posterior
                if (!_descontosEntidade.ContainsKey(id))
                {
                    _descontosEntidade[id] = descEntidade;
                }

                // Adiciona a linha normalmente
                int rowIndex = dataGridView1.Rows.Add(false, tipoDoc, numDoc, serie, entidade, id);
                var row = dataGridView1.Rows[rowIndex];

                // Se for uma encomenda já usada, pinta com um estilo mais elegante e desativa o checkbox
                if (_encomendasUsadas.Contains(id))
                {
                    // Mantém a cor alternada mas com tom mais acinzentado
                    System.Drawing.Color baseColor = (rowIndex % 2 == 0) ?
                        System.Drawing.Color.FromArgb(240, 240, 240) :
                        System.Drawing.Color.FromArgb(235, 235, 235);

                    row.DefaultCellStyle.BackColor = baseColor;
                    row.DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(150, 150, 150);
                    row.Cells[0].ReadOnly = true;
                    row.Cells[0].Value = false; // Desmarca se estava marcado
                    row.Cells[0].Style.ForeColor = System.Drawing.Color.FromArgb(180, 180, 180);
                }

                // Criar uma cópia da linha para os dados originais
                var rowCopy = (DataGridViewRow)row.Clone();
                for (int i = 0; i < row.Cells.Count; i++)
                {
                    rowCopy.Cells[i].Value = row.Cells[i].Value;
                }
                _dadosOriginais.Add(rowCopy);

                dadosCabecDocs.Seguinte();
            }
        }


        private StdBELista GetCabecDocs(string cliente)
        {
            var dataInicio = dateTimePicker_inicio.Value.ToString("yyyy-MM-dd");
            var dataFim = dateTimePicker_fim.Value.ToString("yyyy-MM-dd");

            var queryCabecDoc = $@"
SELECT CD.Id, CD.TipoDoc, CD.NumDoc, CD.Serie, CD.Entidade, CD.Data, CD.DescEntidade
FROM CabecDoc AS CD 
INNER JOIN CabecDocStatus AS CDS ON CD.Id = CDS.IdCabecDoc
WHERE CD.Entidade = '{_Cliente}' 
    AND CDS.Fechado <> 1
    AND CD.Data >= '{dataInicio}' 
    AND CD.Data <= '{dataFim}'
    AND (CD.TipoDoc = 'ECL' OR CD.TipoDoc = 'ECO' OR CD.TipoDoc = 'ORC')
ORDER BY CD.Data DESC";

            return _BSO.Consulta(queryCabecDoc);
        }


        private void txt_numdocumento_TextChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        private void PopularComboBoxes()
        {
            // Popular ComboBox TipoDoc
            var tiposDoc = new HashSet<string>();
            var series = new HashSet<string>();

            foreach (var row in _dadosOriginais)
            {
                string tipoDoc = row.Cells[1].Value?.ToString() ?? "";
                string serie = row.Cells[3].Value?.ToString() ?? "";

                if (!string.IsNullOrEmpty(tipoDoc))
                    tiposDoc.Add(tipoDoc);

                if (!string.IsNullOrEmpty(serie))
                    series.Add(serie);
            }

            comboBox_tipoDoc.Items.Clear();
            comboBox_tipoDoc.Items.Add("Todos");
            foreach (string tipo in tiposDoc.OrderBy(x => x))
            {
                comboBox_tipoDoc.Items.Add(tipo);
            }
            comboBox_tipoDoc.SelectedIndex = 0;

            comboBox_serie.Items.Clear();
            comboBox_serie.Items.Add("Todos");
            foreach (string serie in series.OrderBy(x => x))
            {
                comboBox_serie.Items.Add(serie);
            }
            comboBox_serie.SelectedIndex = 0;
        }

        private void comboBox_tipoDoc_SelectedIndexChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        private void comboBox_serie_SelectedIndexChanged(object sender, EventArgs e)
        {
            AplicarFiltros();
        }

        private void AplicarFiltros()
        {
            string filtroNumDoc = txt_numdocumento.Text.Trim().ToLower();
            string filtroTipoDoc = comboBox_tipoDoc.SelectedItem?.ToString();
            string filtroSerie = comboBox_serie.SelectedItem?.ToString();

            dataGridView1.Rows.Clear();

            foreach (var originalRow in _dadosOriginais)
            {
                string numDoc = originalRow.Cells[2].Value?.ToString().ToLower() ?? "";
                string tipoDoc = originalRow.Cells[1].Value?.ToString() ?? "";
                string serie = originalRow.Cells[3].Value?.ToString() ?? "";

                bool passaFiltroNumDoc = string.IsNullOrEmpty(filtroNumDoc) || numDoc.Contains(filtroNumDoc);
                bool passaFiltroTipoDoc = filtroTipoDoc == "Todos" || tipoDoc == filtroTipoDoc;
                bool passaFiltroSerie = filtroSerie == "Todos" || serie == filtroSerie;

                if (passaFiltroNumDoc && passaFiltroTipoDoc && passaFiltroSerie)
                {
                    // Criar uma nova linha baseada na original
                    var newRow = (DataGridViewRow)originalRow.Clone();
                    for (int i = 0; i < originalRow.Cells.Count; i++)
                    {
                        newRow.Cells[i].Value = originalRow.Cells[i].Value;
                    }

                    // Aplicar estilos para encomendas já usadas
                    string id = newRow.Cells[5].Value?.ToString();
                    if (_encomendasUsadas.Contains(id))
                    {
                        int rowIndex = dataGridView1.Rows.Add(newRow);
                        var addedRow = dataGridView1.Rows[rowIndex];

                        System.Drawing.Color baseColor = (rowIndex % 2 == 0) ?
                            System.Drawing.Color.FromArgb(240, 240, 240) :
                            System.Drawing.Color.FromArgb(235, 235, 235);

                        addedRow.DefaultCellStyle.BackColor = baseColor;
                        addedRow.DefaultCellStyle.ForeColor = System.Drawing.Color.FromArgb(150, 150, 150);
                        addedRow.Cells[0].ReadOnly = true;
                        addedRow.Cells[0].Value = false;
                        addedRow.Cells[0].Style.ForeColor = System.Drawing.Color.FromArgb(180, 180, 180);
                    }
                    else
                    {
                        dataGridView1.Rows.Add(newRow);
                    }
                }
            }
        }


        private void dateTimePicker_inicio_ValueChanged(object sender, EventArgs e)
        {
            RecarregarDados();
        }

        private void dateTimePicker_fim_ValueChanged(object sender, EventArgs e)
        {
            RecarregarDados();
        }

        private void RecarregarDados()
        {
            // Guardar os valores selecionados nos filtros
            string tipoDocSelecionado = comboBox_tipoDoc.SelectedItem?.ToString();
            string serieSelecionada = comboBox_serie.SelectedItem?.ToString();

            // Limpar descontos anteriores
            _descontosEntidade.Clear();

            var dadosCabecDocs = GetCabecDocs(_Cliente);
            CarregarDadosDataGrid(dadosCabecDocs);
            PopularComboBoxes();

            // Restaurar os valores selecionados se ainda existirem nas opções
            if (!string.IsNullOrEmpty(tipoDocSelecionado) && comboBox_tipoDoc.Items.Contains(tipoDocSelecionado))
            {
                comboBox_tipoDoc.SelectedItem = tipoDocSelecionado;
            }

            if (!string.IsNullOrEmpty(serieSelecionada) && comboBox_serie.Items.Contains(serieSelecionada))
            {
                comboBox_serie.SelectedItem = serieSelecionada;
            }
        }

        public double GetDescontoEntidade(string idDocumento)
        {
            return _descontosEntidade.ContainsKey(idDocumento) ? (double)_descontosEntidade[idDocumento] : 0.0;
        }

        private void button1_Click_1(object sender, EventArgs e)
        {
            EncomendasSelecionadas.Clear();


           

            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (!row.IsNewRow && Convert.ToBoolean(row.Cells[0].Value))
                {
                    string id = row.Cells[5].Value?.ToString();
                    if (!string.IsNullOrEmpty(id) && !_encomendasUsadas.Contains(id))
                    {
                        EncomendasSelecionadas.Add(id);
                    }
                }
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
