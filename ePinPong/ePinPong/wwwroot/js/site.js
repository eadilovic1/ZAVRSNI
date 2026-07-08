// 🏓 ePinPong - Klijentska Interaktivnost

document.addEventListener("DOMContentLoaded", function () {
    console.log("ePinPong učitan uspješno!");

    // Automatsko zatvaranje alert obavještenja nakon 5 sekundi
    const alerts = document.querySelectorAll('.alert-dismissible');
    alerts.forEach(function (alert) {
        setTimeout(function () {
            const closeBtn = alert.querySelector('.btn-close');
            if (closeBtn) {
                closeBtn.click();
            }
        }, 5000);
    });
});
