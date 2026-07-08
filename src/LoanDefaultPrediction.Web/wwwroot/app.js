let pfiChart = null;

document.addEventListener('DOMContentLoaded', () => {
    // Fetch initial database stats and active model status
    fetchStats();

    // Event listeners
    document.getElementById('predict-form').addEventListener('submit', handlePrediction);
    document.getElementById('btn-train').addEventListener('click', handleTraining);
    document.getElementById('btn-ingest').addEventListener('click', handleIngestion);
    document.getElementById('btn-kaggle-ingest').addEventListener('click', handleKaggleIngestion);
    document.getElementById('btn-clear').addEventListener('click', handleDatabaseClear);

    // Sync custom file input text
    document.getElementById('csv-file').addEventListener('change', (e) => {
        const fileName = e.target.files[0]?.name || 'Choose CSV File';
        document.querySelector('.custom-file-upload').textContent = fileName;
    });
});

async function fetchStats() {
    try {
        const response = await fetch('/api/database-stats');
        const data = await response.json();

        // Update Summary Stats
        document.getElementById('stat-total').textContent = data.totalRecords.toLocaleString();
        document.getElementById('stat-rate').textContent = `${(data.defaultRate * 100).toFixed(1)}%`;
        
        const progressBar = document.getElementById('stat-progress-bar');
        progressBar.style.width = `${data.defaultRate * 100}%`;

        // Update Active Model Performance
        const activeModelText = document.getElementById('active-model-status');
        if (data.activeModel) {
            activeModelText.textContent = `Model #${data.activeModel.id} Active`;
            activeModelText.className = 'badge badge-success';
            
            document.getElementById('model-accuracy').textContent = `${(data.activeModel.accuracy * 100).toFixed(2)}%`;
            document.getElementById('model-auc').textContent = data.activeModel.areaUnderRoc.toFixed(4);
            document.getElementById('model-precision').textContent = `${(data.activeModel.precision * 100).toFixed(2)}%`;
            document.getElementById('model-recall').textContent = `${(data.activeModel.recall * 100).toFixed(2)}%`;

            const cm = data.activeModel.confusionMatrix || {};
            document.getElementById('matrix-tn').textContent = (cm.TN || 0).toLocaleString();
            document.getElementById('matrix-fn').textContent = (cm.FN || 0).toLocaleString();
            document.getElementById('matrix-fp').textContent = (cm.FP || 0).toLocaleString();
            document.getElementById('matrix-tp').textContent = (cm.TP || 0).toLocaleString();
            
            const trainedDate = new Date(data.activeModel.trainedAt);
            document.getElementById('model-trained-at').textContent = trainedDate.toLocaleString();

            // Populate PFI Chart
            if (data.activeModel.pfiMetrics) {
                renderPfiChart(data.activeModel.pfiMetrics);
                document.getElementById('chart-placeholder').style.opacity = '0';
                document.getElementById('chart-placeholder').style.pointerEvents = 'none';
            }
        } else {
            activeModelText.textContent = 'No Active Model';
            activeModelText.className = 'badge';
            document.getElementById('model-accuracy').textContent = '0.00%';
            document.getElementById('model-auc').textContent = '0.0000';
            document.getElementById('model-precision').textContent = '0.00%';
            document.getElementById('model-recall').textContent = '0.00%';
            document.getElementById('matrix-tn').textContent = '0';
            document.getElementById('matrix-fn').textContent = '0';
            document.getElementById('matrix-fp').textContent = '0';
            document.getElementById('matrix-tp').textContent = '0';
            document.getElementById('model-trained-at').textContent = '-';
            
            if (pfiChart) {
                pfiChart.destroy();
                pfiChart = null;
            }
            document.getElementById('chart-placeholder').style.opacity = '1';
            document.getElementById('chart-placeholder').style.pointerEvents = 'all';
        }
    } catch (error) {
        console.error('Error fetching database stats:', error);
    }
}

function renderPfiChart(pfiMetrics) {
    const ctx = document.getElementById('pfiChart').getContext('2d');
    
    // Sort features by importance
    const sortedFeatures = Object.entries(pfiMetrics)
        .sort((a, b) => b[1] - a[1]);
        
    const labels = sortedFeatures.map(item => item[0]);
    const values = sortedFeatures.map(item => item[1]);

    if (pfiChart) {
        pfiChart.destroy();
    }

    pfiChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Relative Feature Importance (%)',
                data: values,
                backgroundColor: '#2563eb', // solid blue
                borderWidth: 0,
                barThickness: 16
            }]
        },
        options: {
            indexAxis: 'y', // Horizontal bars
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    backgroundColor: '#ffffff',
                    titleFont: { family: 'Outfit', weight: 'bold' },
                    titleColor: '#0f172a',
                    bodyFont: { family: 'Outfit' },
                    bodyColor: '#0f172a',
                    borderColor: '#cbd5e1',
                    borderWidth: 1,
                    callbacks: {
                        label: function(context) {
                            return ` ${context.parsed.x.toFixed(2)}% drop contribution`;
                        }
                    }
                }
            },
            scales: {
                x: {
                    grid: {
                        color: '#cbd5e1'
                    },
                    ticks: {
                        color: '#64748b',
                        font: { family: 'Outfit', size: 10 }
                    },
                    max: 100
                },
                y: {
                    grid: {
                        display: false
                    },
                    ticks: {
                        color: '#f8fafc',
                        font: { family: 'Outfit', size: 11, weight: 500 }
                    }
                }
            }
        }
    });
}

async function handleIngestion() {
    const fileInput = document.getElementById('csv-file');
    const file = fileInput.files[0];
    if (!file) {
        alert('Please choose a CSV file first.');
        return;
    }

    const progressDiv = document.getElementById('ingest-progress');
    const statusText = document.getElementById('ingest-status-text');
    const ingestBtn = document.getElementById('btn-ingest');

    progressDiv.style.display = 'flex';
    statusText.textContent = 'Uploading and validating CSV...';
    ingestBtn.disabled = true;

    const formData = new FormData();
    formData.append('file', file);

    try {
        const response = await fetch('/api/ingest', {
            method: 'POST',
            body: formData
        });
        
        if (!response.ok) {
            const errData = await response.json();
            throw new Error(errData.error || 'Failed to ingest file.');
        }

        const data = await response.json();
        statusText.textContent = 'Ingest complete!';
        
        alert(`Ingestion Successful!\n- Total records processed: ${data.totalRecords}\n- Validated & Saved: ${data.validatedRecords}\n- Failed/Skipped: ${data.failedRecords}`);
        
        // Reset file input
        fileInput.value = '';
        document.querySelector('.custom-file-upload').textContent = 'Choose CSV File';
        
        // Refresh statistics
        await fetchStats();
    } catch (error) {
        alert(`Ingestion Error: ${error.message}`);
    } finally {
        progressDiv.style.display = 'none';
        ingestBtn.disabled = false;
    }
}

async function handleTraining() {
    const trainBtn = document.getElementById('btn-train');
    trainBtn.classList.add('loading');
    trainBtn.disabled = true;

    try {
        const response = await fetch('/api/train', { method: 'POST' });
        if (!response.ok) {
            const errData = await response.json();
            throw new Error(errData.Error || 'Failed to execute training pipeline.');
        }

        const result = await response.json();
        
        alert(`Model Training Succeeded!\n- New Active Model ID: #${result.runId}\n- Accuracy: ${(result.accuracy * 100).toFixed(2)}%\n- AUC ROC: ${result.areaUnderRoc.toFixed(4)}\n- PFI Explainability successfully updated.`);
        
        // Refresh stats
        await fetchStats();
    } catch (error) {
        alert(`Training Pipeline Failed: ${error.message}`);
    } finally {
        trainBtn.classList.remove('loading');
        trainBtn.disabled = false;
    }
}

async function handlePrediction(e) {
    e.preventDefault();

    const predictBtn = document.getElementById('btn-predict');
    predictBtn.disabled = true;
    predictBtn.textContent = 'Evaluating...';

    // Map inputs to LoanData class schema
    const payload = {
        age: parseFloat(document.getElementById('input-age').value),
        income: parseFloat(document.getElementById('input-income').value),
        loanAmount: parseFloat(document.getElementById('input-amount').value),
        creditScore: parseFloat(document.getElementById('input-score').value),
        monthsEmployed: parseFloat(document.getElementById('input-employed').value),
        interestRate: parseFloat(document.getElementById('input-rate').value),
        loanTerm: parseFloat(document.getElementById('input-term').value),
        debtToIncomeRatio: parseFloat(document.getElementById('input-dti').value)
    };

    try {
        const response = await fetch('/api/predict', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            const errData = await response.json();
            throw new Error(errData.Error || 'Failed to calculate prediction.');
        }

        const data = await response.json();

        // Render Results Card
        const resultCard = document.getElementById('result-card');
        resultCard.classList.remove('hidden');

        // Animate flat risk progress bar
        const riskBar = document.getElementById('risk-bar');
        const riskPct = document.getElementById('risk-percentage');
        
        const probability = data.probability;
        const percentage = Math.round(probability * 100);
        
        riskBar.style.width = `${percentage}%`;
        riskPct.textContent = `${percentage}%`;

        // Update colors based on risk severity (solid hex colors, no gradients)
        let riskColor = '#059669'; // emerald green
        if (percentage > 50) {
            riskColor = '#dc2626'; // solid red
        } else if (percentage > 25) {
            riskColor = '#d97706'; // warning orange
        }
        riskBar.style.backgroundColor = riskColor;
        riskPct.style.color = riskColor;

        // Render decision badge
        const badge = document.getElementById('verdict-badge');
        badge.textContent = data.decision;
        if (data.predictedDefault) {
            badge.className = 'verdict-badge rejected';
        } else {
            badge.className = 'verdict-badge approved';
        }

        // Render verdict text and explanations
        document.getElementById('verdict-msg').textContent = data.message;

        const listContainer = document.getElementById('explanations-list');
        listContainer.innerHTML = '';
        if (data.explanations && data.explanations.length > 0) {
            data.explanations.forEach(exp => {
                const li = document.createElement('li');
                li.textContent = exp;
                listContainer.appendChild(li);
            });
        } else {
            const li = document.createElement('li');
            li.textContent = "The applicant's financial attributes present standard portfolio risk profiles.";
            listContainer.appendChild(li);
        }

        // Scroll to results on mobile
        resultCard.scrollIntoView({ behavior: 'smooth' });

    } catch (error) {
        alert(error.message);
    } finally {
        predictBtn.disabled = false;
        predictBtn.textContent = 'Calculate Risk Score';
    }
}

async function handleKaggleIngestion() {
    const datasetPathInput = document.getElementById('kaggle-path');
    const datasetPath = datasetPathInput.value.trim();
    if (!datasetPath) {
        alert('Please enter a Kaggle dataset path in the format: owner/dataset-name');
        return;
    }

    const progressDiv = document.getElementById('ingest-progress');
    const statusText = document.getElementById('ingest-status-text');
    const pullBtn = document.getElementById('btn-kaggle-ingest');

    progressDiv.style.display = 'flex';
    statusText.textContent = 'Downloading zip and extracting CSV from Kaggle API...';
    pullBtn.disabled = true;

    try {
        const response = await fetch('/api/kaggle-ingest', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ datasetPath })
        });
        
        if (!response.ok) {
            const errData = await response.json();
            throw new Error(errData.Error || 'Failed to download and ingest dataset.');
        }

        const data = await response.json();
        statusText.textContent = 'Ingestion complete!';
        
        alert(`Kaggle Ingestion Succeeded!\n- Total records processed: ${data.totalRecords}\n- Validated & Saved: ${data.validatedRecords}\n- Failed/Skipped: ${data.failedRecords}`);
        
        // Reset path input
        datasetPathInput.value = '';
        
        // Refresh statistics
        await fetchStats();
    } catch (error) {
        alert(`Kaggle Ingest Error: ${error.message}`);
    } finally {
        progressDiv.style.display = 'none';
        pullBtn.disabled = false;
    }
}

async function handleDatabaseClear() {
    if (!confirm("Are you sure you want to permanently delete all ingested loan records and trained models?")) {
        return;
    }

    const clearBtn = document.getElementById('btn-clear');
    clearBtn.disabled = true;

    try {
        const response = await fetch('/api/database-clear', { method: 'POST' });
        if (!response.ok) {
            throw new Error('Failed to reset database.');
        }

        alert('Database successfully reset!');
        await fetchStats();
    } catch (error) {
        alert(error.message);
    } finally {
        clearBtn.disabled = false;
    }
}
