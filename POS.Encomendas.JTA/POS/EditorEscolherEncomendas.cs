using StdBE100;
using System;
using System.Collections.Generic;
using System.Windows.Forms;


namespace POS.Encomendas.JTA.POS
{
    public partial class EditorEscolherEncomendas : Form
    {

        private ErpBS100.ErpBS _BSO;
        private StdPlatBS100.StdBSInterfPub _PSO;
        private string _Cliente;
        private HashSet<string> _encomendasUsadas;

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
            var dadosCabecDocs = GetCabecDocs(_Cliente);
            CarregarDadosDataGrid(dadosCabecDocs);
            txt_nomeCliente.Text = _Cliente;
        }

        private void CarregarDadosDataGrid(StdBELista dadosCabecDocs)
        {
            dadosCabecDocs.Inicio();
            dataGridView1.Rows.Clear();

            while (!dadosCabecDocs.NoFim())
            {
                string id = dadosCabecDocs.DaValor<string>("Id");
                string tipoDoc = dadosCabecDocs.DaValor<string>("TipoDoc");
                string numDoc = dadosCabecDocs.DaValor<string>("NumDoc");
                string serie = dadosCabecDocs.DaValor<string>("Serie");
                string entidade = dadosCabecDocs.DaValor<string>("Entidade");

                // Adiciona a linha normalmente
                int rowIndex = dataGridView1.Rows.Add(false, tipoDoc, numDoc, serie, entidade, id);
                var row = dataGridView1.Rows[rowIndex];

                // Se for uma encomenda já usada, pinta de cinzento e desativa o checkbox
                if (_encomendasUsadas.Contains(id))
                {
                    row.DefaultCellStyle.BackColor = System.Drawing.Color.LightGray;
                    row.Cells[0].ReadOnly = true;
                    row.Cells[0].Value = false; // Desmarca se estava marcado
                    row.Cells[0].Style.ForeColor = System.Drawing.Color.DarkGray;
                }

                dadosCabecDocs.Seguinte();
            }
        }


        private StdBELista GetCabecDocs(string cliente)
        {
            var queryCabecDoc = $@"
                SELECT * FROM CabecDoc AS CD 
                INNER JOIN CabecDocStatus AS CDS ON CD.Id = CDS.IdCabecDoc
                WHERE CD.TipoDoc = 'ECL' AND CD.Entidade = '{_Cliente}' AND CDS.Fechado <> 1
                ORDER BY CD.Data DESC";

            return _BSO.Consulta(queryCabecDoc);
        }


        private void txt_numdocumento_TextChanged(object sender, EventArgs e)
        {
            string filtro = txt_numdocumento.Text.Trim().ToLower();

            foreach (DataGridViewRow row in dataGridView1.Rows)
            {
                if (row.IsNewRow) continue;

                // A coluna 2 é onde está o NumDoc (conforme CarregarDadosDataGrid)
                string numDoc = row.Cells[2].Value?.ToString().ToLower() ?? "";

                if (numDoc.Contains(filtro))
                {
                    row.Visible = true;
                }
                else
                {
                    row.Visible = false;
                }
            }
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
