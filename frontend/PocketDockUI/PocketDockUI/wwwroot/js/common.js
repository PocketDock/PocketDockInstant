function updateStats() {
    $.getJSON('/api/metrics').then(res => {
        $('#amountofservers').html(res[0]);
        $('#amountofplayers').html(res[1]);
    });
}

updateStats();
setInterval(function () {
    updateStats();
}, 30000);

function showAlert(msg) {
    $('#alert').show();
    $('#alerttext').html(msg);
    $("#alert").addClass("in");
}